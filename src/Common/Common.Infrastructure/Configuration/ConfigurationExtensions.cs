using Common.Application.Interfaces;
using Common.Infrastructure.Caching;
using Common.Infrastructure.Logging;
using Common.Infrastructure.Messaging;
using Common.Infrastructure.Messaging.Outbox;
using Common.Infrastructure.Persistence;
using Common.Infrastructure.Services;
using Common.Infrastructure.Telemetry;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using StackExchange.Redis;

namespace Common.Infrastructure.Configuration;

public static class ConfigurationExtensions
{
    public static IServiceCollection AddCommonInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        string serviceName)
    {
        // Redis
        var redisConnectionString = configuration["Redis:ConnectionString"];
        if (!string.IsNullOrEmpty(redisConnectionString))
        {
            services.AddSingleton<IConnectionMultiplexer>(
                ConnectionMultiplexer.Connect(new ConfigurationOptions
                {
                    EndPoints = { redisConnectionString },
                    AbortOnConnectFail = false,
                }));
            services.AddScoped<ICacheService, RedisCacheService>();
        }

        // RabbitMQ + MassTransit
        var rabbitMqHost = configuration["RabbitMq:Host"];
        if (!string.IsNullOrEmpty(rabbitMqHost))
        {
            services.AddMassTransit(x =>
            {
                x.AddConsumers(typeof(BaseDbContext).Assembly);

                x.UsingRabbitMq((context, cfg) =>
                {
                    cfg.Host(rabbitMqHost, hostConfig =>
                    {
                        hostConfig.Username(configuration["RabbitMq:Username"] ?? "guest");
                        hostConfig.Password(configuration["RabbitMq:Password"] ?? "guest");
                    });

                    cfg.ReceiveEndpoint(serviceName, e =>
                    {
                        e.ConfigureConsumers(context);
                        e.UseMessageRetry(r => r.Immediate(3));
                    });
                });
            });

            services.AddScoped<IEventPublisher, RabbitMqEventPublisher>();
        }

        // Exchange rate service — use real Redis-backed one if Redis is available,
        // otherwise fall back to a static rate.
        // Note: ProductService registers the real ExchangeRateService separately.
        if (string.IsNullOrEmpty(redisConnectionString))
        {
            // Redis is not available → register fallback stub so consumers can still resolve IExchangeRateService
            services.AddScoped<IExchangeRateService, FallbackExchangeRateService>();
        }

        // OpenTelemetry
        services.AddCmaTelemetry(configuration, serviceName);

        return services;
    }

    public static IHostBuilder UseCommonLogging(this IHostBuilder hostBuilder)
    {
        return hostBuilder.UseSerilogLogging();
    }
}
