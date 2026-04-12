using Common.Application.Interfaces;
using Common.Infrastructure.Caching;
using Common.Infrastructure.Logging;
using Common.Infrastructure.Messaging;
using Common.Infrastructure.Messaging.Outbox;
using Common.Infrastructure.Persistence;
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
        // Database — register BaseDbContext as open generic
        services.AddDbContext<BaseDbContext>((sp, options) =>
        {
            var connectionString = configuration.GetConnectionString("DefaultConnection");
            options.UseMySql(connectionString!, ServerVersion.AutoDetect(connectionString));
        });

        // Redis
        var redisConnectionString = configuration["Redis:ConnectionString"];
        if (!string.IsNullOrEmpty(redisConnectionString))
        {
            services.AddSingleton<IConnectionMultiplexer>(
                ConnectionMultiplexer.Connect(redisConnectionString));
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

            // Outbox pattern: repository + background processor
            services.AddScoped<IOutboxRepository, OutboxRepository>();
            services.AddHostedService<OutboxProcessor>();
        }

        // OpenTelemetry
        services.AddCmaTelemetry(configuration, serviceName);

        return services;
    }

    public static IHostBuilder UseCommonLogging(this IHostBuilder hostBuilder)
    {
        return hostBuilder
            .UseSerilogLogging()
            .UseSerilogRequestLogging();
    }
}
