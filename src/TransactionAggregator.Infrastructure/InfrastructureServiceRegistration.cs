using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Polly;
using Polly.Retry;
using System.Text;
using TransactionAggregator.Application.Common.Interfaces;
using TransactionAggregator.Domain.Interfaces;
using TransactionAggregator.Infrastructure.BackgroundServices;
using TransactionAggregator.Infrastructure.Caching;
using TransactionAggregator.Infrastructure.Categorization;
using TransactionAggregator.Infrastructure.Ingestion;
using TransactionAggregator.Infrastructure.Persistence;
using TransactionAggregator.Infrastructure.Services;

namespace TransactionAggregator.Infrastructure;

public static class InfrastructureServiceRegistration
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // ── Database ──────────────────────────────────────────────────────
        services.AddDbContext<ApplicationDbContext>(opts =>
            opts.UseSqlServer(
                configuration.GetConnectionString("DefaultConnection"),
                sql => sql.EnableRetryOnFailure(3)));

        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());

        // ── Repositories & UoW ────────────────────────────────────────────
        services.AddScoped<ITransactionRepository, TransactionRepository>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<ICategorizationRuleRepository, CategorizationRuleRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // ── Categorization ────────────────────────────────────────────────
        services.AddSingleton<ICategorizationStrategy, DatabaseRuleCategorizationStrategy>();
        services.AddSingleton<ICategorizationService, CategorizationService>();

        // ── Caching ───────────────────────────────────────────────────────
        var redisConnString = configuration.GetConnectionString("Redis") ?? "localhost:6379,abortConnect=false";
        services.AddSingleton<StackExchange.Redis.IConnectionMultiplexer>(sp =>
            StackExchange.Redis.ConnectionMultiplexer.Connect(redisConnString));
            
        services.AddStackExchangeRedisCache(opts =>
        {
            opts.Configuration = redisConnString;
            opts.InstanceName = "TransactionAgg:";
        });
        services.AddSingleton<ICacheService, RedisCacheService>();

        // ── HTTP Clients ──────────────────────────────────────────────────
        services.AddHttpClient();

        // ── Data Sources ──────────────────────────────────────────────────
        services.AddScoped<ITransactionDataSource, RestApiTransactionSource>();
        services.AddScoped<ITransactionDataSource, CsvFileTransactionSource>();

        // ── Resilience ────────────────────────────────────────────────────
        services.AddResiliencePipeline("ExternalSource", builder =>
        {
            builder.AddRetry(new RetryStrategyOptions
            {
                ShouldHandle = new PredicateBuilder().Handle<Exception>(),
                Delay = TimeSpan.FromSeconds(2),
                MaxRetryAttempts = 3,
                BackoffType = DelayBackoffType.Exponential
            });
        });

        // ── Options ───────────────────────────────────────────────────────
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<FileSourceOptions>(configuration.GetSection(FileSourceOptions.SectionName));
        services.Configure<IngestionJobOptions>(configuration.GetSection(IngestionJobOptions.SectionName));

        // ── Services ──────────────────────────────────────────────────────
        services.AddScoped<IJwtTokenService, JwtTokenService>();

        // ── Background Services ───────────────────────────────────────────
        services.AddSingleton<IBackgroundTaskQueue>(ctx => new BackgroundTaskQueue(100));
        services.AddHostedService<QueuedHostedService>();

        var ingestionOpts = configuration.GetSection(IngestionJobOptions.SectionName).Get<IngestionJobOptions>();
        if (ingestionOpts?.Enabled != false)
            services.AddHostedService<TransactionIngestionJob>();

        // ── JWT Authentication ────────────────────────────────────────────
        var jwtOpts = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>();
        if (jwtOpts is not null)
        {
            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(opts =>
                {
                    opts.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,
                        ValidIssuer = jwtOpts.Issuer,
                        ValidAudience = jwtOpts.Audience,
                        IssuerSigningKey = new SymmetricSecurityKey(
                            Encoding.UTF8.GetBytes(jwtOpts.SecretKey))
                    };
                });
        }

        services.AddAuthorization(opts =>
        {
            opts.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
            opts.AddPolicy("UserOrAdmin", policy => policy.RequireRole("Admin", "User"));
        });

        return services;
    }
}
