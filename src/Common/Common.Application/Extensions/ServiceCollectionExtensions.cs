using Common.Application.Behaviors;
using Common.Application.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Polly;
using Polly.Retry;
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

    /// <summary>
    /// Adds standard retry (3x exponential backoff) + circuit breaker (5 failures, 30s break)
    /// resilience to an HttpClient using Polly 8 policies + Microsoft.Extensions.Http.Polly.
    /// Named AddCmaStandardResilienceHandler to avoid ambiguity with Microsoft.Extensions.Http.Polly.
    /// </summary>
    public static IHttpClientBuilder AddCmaStandardResilienceHandler(this IHttpClientBuilder builder)
    {
        var retryPolicy = Policy<HttpResponseMessage>
            .Handle<Exception>(ex => ex is not OperationCanceledException)
            .OrResult(r => !r.IsSuccessStatusCode)
            .WaitAndRetryAsync(3, attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)));

        var circuitBreakerPolicy = Policy<HttpResponseMessage>
            .Handle<Exception>(ex => ex is not OperationCanceledException)
            .OrResult(r => !r.IsSuccessStatusCode)
            .CircuitBreakerAsync(5, TimeSpan.FromSeconds(30));

        return builder
            .AddPolicyHandler(retryPolicy)
            .AddPolicyHandler(circuitBreakerPolicy);
    }

    /// <summary>
    /// Adds configurable retry + circuit-breaker resilience to an HttpClient.
    /// Timeout is set on the HttpClient itself via ConfigureHttpClient.
    /// Named AddCmaResilientHttpClient to avoid ambiguity with Microsoft.Extensions.Http.Polly.
    /// </summary>
    public static IHttpClientBuilder AddCmaResilientHttpClient(
        this IHttpClientBuilder builder,
        Action<ResilienceOptions> configure)
    {
        var options = new ResilienceOptions();
        configure(options);

        var retryPolicy = Policy<HttpResponseMessage>
            .Handle<Exception>(ex => ex is not OperationCanceledException)
            .OrResult(r => !r.IsSuccessStatusCode)
            .WaitAndRetryAsync(
                options.MaxRetryAttempts,
                _ => options.BackoffType == BackoffStrategy.Exponential
                    ? TimeSpan.FromSeconds(Math.Pow(2, _))
                    : TimeSpan.FromMilliseconds(100));

        IAsyncPolicy<HttpResponseMessage> cbPolicy;
        if (options.CircuitBreakerFailureRatio > 0)
        {
            cbPolicy = Policy<HttpResponseMessage>
                .Handle<Exception>(ex => ex is not OperationCanceledException)
                .OrResult(r => !r.IsSuccessStatusCode)
                .CircuitBreakerAsync(
                    options.CircuitBreakerMinimumThroughput,
                    options.CircuitBreakerBreakDuration);
        }
        else
        {
            cbPolicy = Policy.NoOpAsync<HttpResponseMessage>();
        }

        return builder
            .AddPolicyHandler(retryPolicy)
            .AddPolicyHandler(cbPolicy);
    }

    public enum BackoffStrategy { Exponential, Constant }

    public class ResilienceOptions
    {
        public int MaxRetryAttempts { get; set; } = 3;
        public BackoffStrategy BackoffType { get; set; } = BackoffStrategy.Exponential;
        public double CircuitBreakerFailureRatio { get; set; }
        public int CircuitBreakerMinimumThroughput { get; set; } = 5;
        public TimeSpan CircuitBreakerBreakDuration { get; set; } = TimeSpan.FromSeconds(30);
    }
}
