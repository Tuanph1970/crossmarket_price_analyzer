using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Polly.Timeout;

namespace Common.Infrastructure.Resilience;

/// <summary>
/// Central Polly resilience policies shared across all services.
/// Configured via IConfiguration; override in appsettings.json per environment.
/// </summary>
public static class ResiliencePolicies
{
    /// <summary>
    /// HTTP calls: retry on 5xx/timeout, circuit breaker, per-request timeout.
    /// </summary>
    public static ResiliencePipeline<HttpResponseMessage> HttpPipeline(
        ILogger? logger = null)
    {
        return new ResiliencePipelineBuilder<HttpResponseMessage>()
            .AddTimeout(TimeSpan.FromSeconds(30))
            .AddRetry(new RetryStrategyOptions<HttpResponseMessage>
            {
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromSeconds(1),
                BackoffType = DelayBackoffType.Exponential,
                ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                    .Handle<TimeoutRejectedException>()
                    .Handle<HttpRequestException>()
                    .HandleResult(r => !r.IsSuccessStatusCode && (int)r.StatusCode >= 500),
                OnRetry = args =>
                {
                    logger?.LogWarning(args.Outcome.Exception,
                        "HTTP retry {Attempt}/3 after {Delay}ms — {Status}",
                        args.AttemptNumber, args.RetryDelay.TotalMilliseconds,
                        args.Outcome.Result?.StatusCode.ToString() ?? "exception");
                    return ValueTask.CompletedTask;
                },
            })
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions<HttpResponseMessage>
            {
                FailureRatio = 0.5,
                MinimumThroughput = 5,
                SamplingDuration = TimeSpan.FromSeconds(30),
                BreakDuration = TimeSpan.FromSeconds(30),
                ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                    .Handle<HttpRequestException>()
                    .HandleResult(r => !r.IsSuccessStatusCode),
                OnOpened = args =>
                {
                    logger?.LogWarning(
                        "Circuit breaker OPENED for HTTP — breaking for {Duration}s",
                        args.BreakDuration.TotalSeconds);
                    return ValueTask.CompletedTask;
                },
                OnClosed = _ =>
                {
                    logger?.LogInformation("Circuit breaker CLOSED — HTTP calls resuming");
                    return ValueTask.CompletedTask;
                },
            })
            .Build();
    }

    /// <summary>
    /// RabbitMQ publish: retry on transient failures, circuit breaker.
    /// </summary>
    public static ResiliencePipeline RabbitMqPipeline(ILogger? logger = null)
    {
        return new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromMilliseconds(200),
                BackoffType = DelayBackoffType.Exponential,
                ShouldHandle = new PredicateBuilder()
                    .Handle<TimeoutRejectedException>()
                    .Handle<Exception>(ex => ex is not OperationCanceledException),
                OnRetry = args =>
                {
                    logger?.LogWarning(args.Outcome.Exception,
                        "RabbitMQ retry {Attempt}/3 after {Delay}ms",
                        args.AttemptNumber, args.RetryDelay.TotalMilliseconds);
                    return ValueTask.CompletedTask;
                },
            })
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                FailureRatio = 0.5,
                MinimumThroughput = 10,
                SamplingDuration = TimeSpan.FromSeconds(30),
                BreakDuration = TimeSpan.FromSeconds(60),
                ShouldHandle = new PredicateBuilder()
                    .Handle<Exception>(ex => ex is not OperationCanceledException),
                OnOpened = args =>
                {
                    logger?.LogWarning(
                        "RabbitMQ circuit breaker OPENED — breaking for {Duration}s",
                        args.BreakDuration.TotalSeconds);
                    return ValueTask.CompletedTask;
                },
            })
            .Build();
    }

    /// <summary>
    /// Redis: short retry + circuit breaker for cache failures.
    /// </summary>
    public static ResiliencePipeline RedisPipeline(ILogger? logger = null)
    {
        return new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 2,
                Delay = TimeSpan.FromMilliseconds(100),
                BackoffType = DelayBackoffType.Constant,
                ShouldHandle = new PredicateBuilder()
                    .Handle<TimeoutRejectedException>()
                    .Handle<Exception>(ex => ex is not OperationCanceledException),
                OnRetry = args =>
                {
                    logger?.LogWarning(args.Outcome.Exception,
                        "Redis retry {Attempt}/2", args.AttemptNumber);
                    return ValueTask.CompletedTask;
                },
            })
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                FailureRatio = 0.5,
                MinimumThroughput = 5,
                SamplingDuration = TimeSpan.FromSeconds(20),
                BreakDuration = TimeSpan.FromSeconds(30),
                ShouldHandle = new PredicateBuilder()
                    .Handle<Exception>(ex => ex is not OperationCanceledException),
                OnOpened = args =>
                {
                    logger?.LogWarning(
                        "Redis circuit breaker OPENED — breaking for {Duration}s",
                        args.BreakDuration.TotalSeconds);
                    return ValueTask.CompletedTask;
                },
            })
            .Build();
    }

}
