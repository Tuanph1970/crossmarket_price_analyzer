using Common.Application.Interfaces;
using Common.Domain.Messaging.Outbox;
using MassTransit;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;

namespace Common.Infrastructure.Messaging;

/// <summary>
/// MassTransit/RabbitMQ implementation of IEventPublisher.
/// Wrapped with Polly retry + circuit breaker for resilience.
///
/// Supports an optional dual-write mode where messages are also persisted to the
/// outbox table for at-least-once delivery when OutboxProcessor is enabled.
/// </summary>
public class RabbitMqEventPublisher : IEventPublisher
{
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly IOutboxRepository? _outboxRepo;       // null when dual-write is disabled
    private readonly ILogger<RabbitMqEventPublisher> _logger;
    private readonly ResiliencePipeline _resiliencePipeline;

    /// <param name="publishEndpoint">MassTransit publish endpoint (required).</param>
    /// <param name="logger">Logger instance.</param>
    /// <param name="outboxRepo">
    /// Optional outbox repository for dual-write mode. When provided, every
    /// published message is also written to the outbox table as a safety net.
    /// </param>
    public RabbitMqEventPublisher(
        IPublishEndpoint publishEndpoint,
        ILogger<RabbitMqEventPublisher> logger,
        IOutboxRepository? outboxRepo = null)
    {
        _publishEndpoint = publishEndpoint;
        _logger = logger;
        _outboxRepo = outboxRepo;
        _resiliencePipeline = BuildPipeline(logger);
    }

    public async Task PublishAsync<T>(T @event, CancellationToken ct = default) where T : class
    {
        _logger.LogDebug("Publishing event {EventType}", typeof(T).Name);

        await _resiliencePipeline.ExecuteAsync(async token =>
        {
            // Primary path: direct MassTransit publish
            await _publishEndpoint.Publish(@event, token);

            // Dual-write: also persist to outbox for at-least-once guarantees
            if (_outboxRepo != null)
            {
                var payload = System.Text.Json.JsonSerializer.Serialize(@event);
                var outboxMsg = OutboxMessage.Create(typeof(T).AssemblyQualifiedName!, payload);
                await _outboxRepo.AddAsync(outboxMsg, token);
                _logger.LogDebug("Outbox dual-write: {EventType} written to outbox (Id={Id})",
                    typeof(T).Name, outboxMsg.Id);
            }
        }, ct);
    }

    private static ResiliencePipeline BuildPipeline(ILogger logger)
    {
        return new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromMilliseconds(200),
                BackoffType = DelayBackoffType.Exponential,
                ShouldHandle = new PredicateBuilder()
                    .Handle<Exception>(ex => ex is not OperationCanceledException),
                OnRetry = args =>
                {
                    logger.LogWarning(args.Outcome.Exception,
                        "RabbitMQ publish retry {Attempt}/3 after {Delay}ms",
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
                    logger.LogWarning("RabbitMQ circuit breaker OPENED — breaking for {Duration}s",
                        args.BreakDuration.TotalSeconds);
                    return ValueTask.CompletedTask;
                },
            })
            .Build();
    }
}
