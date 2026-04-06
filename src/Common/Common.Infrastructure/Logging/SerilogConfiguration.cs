using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;

namespace Common.Infrastructure.Logging;

/// <summary>
/// Centralized Serilog configuration for all microservices.
/// </summary>
public static class SerilogConfiguration
{
    private static readonly string[] ExcludedPaths =
        ["/health", "/healthz", "/ready", "/metrics"];

    public static IHostBuilder UseSerilogLogging(this IHostBuilder hostBuilder)
    {
        return hostBuilder.UseSerilog((context, config) =>
        {
            var env = context.HostingEnvironment;
            var elkUri = context.Configuration["Elasticsearch:Uri"];
            var serviceName = context.Configuration["App:ServiceName"] ?? "CmaService";

            config
                .MinimumLevel.Information()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
                .MinimumLevel.Override("System", LogEventLevel.Warning)
                .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
                .Enrich.FromLogContext()
                .Enrich.WithMachineName()
                .Enrich.WithEnvironmentName()
                .Enrich.WithProperty("Application", serviceName)
                .Enrich.WithProperty("InstanceId", Environment.MachineName)
                .WriteTo.Console(
                    outputTemplate:
                    "[{Timestamp:HH:mm:ss} {Level:u3}] {ServiceName} {Message:lj}{NewLine}{Exception}")
                .WriteTo.Debug();

            // Elasticsearch sink for production (optional)
            if (!string.IsNullOrEmpty(elkUri) && env.IsProduction())
            {
                try
                {
                    config.WriteTo.Elasticsearch(
                        new Serilog.Sinks.Elasticsearch.ElasticsearchSinkOptions(new Uri(elkUri))
                    {
                        AutoRegisterTemplate = true,
                        IndexFormat = $"cma-logs-{serviceName.ToLowerInvariant()}-{{0:yyyy.MM.dd}}",
                    });
                }
                catch
                {
                    // Fall back silently if Elasticsearch is unavailable
                }
            }
        });
    }

    public static IHostBuilder UseSerilogRequestLogging(this IHostBuilder hostBuilder)
    {
        return hostBuilder.UseSerilogRequestLogging();
    }
}
