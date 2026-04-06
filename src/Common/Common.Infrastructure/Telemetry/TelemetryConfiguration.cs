using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Common.Infrastructure.Telemetry;

/// <summary>
/// OpenTelemetry configuration for distributed tracing across all services.
/// </summary>
public static class TelemetryConfiguration
{
    public static IServiceCollection AddCmaTelemetry(
        this IServiceCollection services,
        IConfiguration configuration,
        string serviceName)
    {
        var otlpEndpoint = configuration["OpenTelemetry:OtlpEndpoint"];

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(serviceName: serviceName, serviceVersion: "1.0.0"))
            .WithTracing(tracing =>
            {
                tracing
                    .AddAspNetCoreInstrumentation(options =>
                    {
                        options.RecordException = true;
                        options.Filter = httpCtx =>
                            !(httpCtx.Request.Path.Value ?? "").StartsWith("/health",
                                StringComparison.OrdinalIgnoreCase);
                    })
                    .AddHttpClientInstrumentation(options => options.RecordException = true)
                    .AddConsoleExporter();
            });

        return services;
    }
}
