using Common.Application.Behaviors;
using Common.Application.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace Common.Application.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers all Common.Application services: MediatR handlers, pipeline behaviors,
    /// and FluentValidation validators from the calling assembly.
    /// </summary>
    public static IServiceCollection AddCommonApplication(this IServiceCollection services)
    {
        // Register MediatR
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(Assembly.GetCallingAssembly());

            // Register all pipeline behaviors
            cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
            cfg.AddOpenBehavior(typeof(CachingBehavior<,>));
            cfg.AddOpenBehavior(typeof(PerfBehavior<,>));
        });

        // Register FluentValidation validators from the calling assembly
        services.AddValidatorsFromAssembly(Assembly.GetCallingAssembly());

        return services;
    }
}
