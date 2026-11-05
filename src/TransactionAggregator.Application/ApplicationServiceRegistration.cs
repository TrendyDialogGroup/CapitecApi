using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using TransactionAggregator.Application.Common.Behaviours;

namespace TransactionAggregator.Application;

/// <summary>
/// Registers all Application-layer services via IServiceCollection extension.
/// Called from the API's Program.cs — keeps startup clean.
/// </summary>
public static class ApplicationServiceRegistration
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();

        // AutoMapper 15+ requires a configure action before assembly scanning
        services.AddAutoMapper(cfg => { }, assembly);

        // FluentValidation
        services.AddValidatorsFromAssembly(assembly);

        // MediatR — registers all handlers, behaviours, notifications
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(assembly);

            // Pipeline order matters: Logging → Validation → Handler
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(LoggingBehaviour<,>));
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehaviour<,>));
        });

        return services;
    }
}
