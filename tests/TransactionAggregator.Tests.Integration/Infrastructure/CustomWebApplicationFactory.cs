using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using StackExchange.Redis;
using TransactionAggregator.Application.Common.Interfaces;
using TransactionAggregator.Domain.Enums;
using TransactionAggregator.Infrastructure.Persistence;

namespace TransactionAggregator.Tests.Integration.Infrastructure;

public sealed class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"IntegrationTests-{Guid.NewGuid()}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // Replace SQL Server with a dedicated in-memory database per factory instance.
            RemoveAll(services, typeof(DbContextOptions<ApplicationDbContext>));
            RemoveAll(services, typeof(ApplicationDbContext));

            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseInMemoryDatabase(_databaseName));
            services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());

            // Replace Redis with an in-memory cache double.
            RemoveAll(services, typeof(IConnectionMultiplexer));
            RemoveAll(services, typeof(IDistributedCache));
            RemoveAll(services, typeof(ICacheService));
            services.AddSingleton<IDistributedCache, MemoryDistributedCache>();
            services.AddSingleton<ICacheService, TestCacheService>();

            // Keep QueuedHostedService (needed for /ingest 202 processing) but disable the periodic job.
            foreach (var descriptor in services
                         .Where(d => d.ServiceType == typeof(IHostedService) &&
                                     d.ImplementationType?.Name == "TransactionIngestionJob")
                         .ToList())
            {
                services.Remove(descriptor);
            }

            // Replace flaky external sources with a controllable fake.
            RemoveAll(services, typeof(ITransactionDataSource));
            services.AddSingleton<FakeTransactionDataSource>();
            services.AddSingleton<ITransactionDataSource>(sp => sp.GetRequiredService<FakeTransactionDataSource>());
        });
    }

    private static void RemoveAll(IServiceCollection services, Type serviceType)
    {
        var descriptors = services.Where(d => d.ServiceType == serviceType).ToList();
        foreach (var descriptor in descriptors)
            services.Remove(descriptor);
    }
}

public sealed class FakeTransactionDataSource : ITransactionDataSource
{
    private IReadOnlyList<RawTransaction> _transactions = [];
    private Exception? _exception;

    public string SourceName => "IntegrationFakeSource";
    public TransactionSource SourceType => TransactionSource.RestApi;

    public void SetTransactions(params RawTransaction[] transactions)
    {
        _exception = null;
        _transactions = transactions;
    }

    public void SetFailure(Exception exception)
    {
        _exception = exception;
        _transactions = [];
    }

    public Task<IReadOnlyList<RawTransaction>> FetchAsync(CancellationToken cancellationToken = default)
    {
        if (_exception is not null)
            throw _exception;

        return Task.FromResult(_transactions);
    }
}

public sealed class TestCacheService : ICacheService
{
    private readonly Dictionary<string, object> _store = new(StringComparer.Ordinal);

    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        if (_store.TryGetValue(key, out var value) && value is T typed)
            return Task.FromResult<T?>(typed);

        return Task.FromResult<T?>(default);
    }

    public Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken cancellationToken = default)
    {
        _store[key] = value!;
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        _store.Remove(key);
        return Task.CompletedTask;
    }

    public Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default)
    {
        foreach (var key in _store.Keys.Where(k => k.StartsWith(prefix, StringComparison.Ordinal)).ToList())
            _store.Remove(key);

        return Task.CompletedTask;
    }

    public async Task<T> GetOrSetAsync<T>(
        string key,
        Func<Task<T>> factory,
        TimeSpan? expiry = null,
        CancellationToken cancellationToken = default)
    {
        var cached = await GetAsync<T>(key, cancellationToken);
        if (cached is not null)
            return cached;

        var value = await factory();
        await SetAsync(key, value, expiry, cancellationToken);
        return value;
    }
}
