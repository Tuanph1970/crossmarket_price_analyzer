using Common.Application.Interfaces;
using Common.Infrastructure.Messaging.Outbox;
using Common.Infrastructure.Persistence;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;

namespace Common.Infrastructure.Messaging.Outbox;

/// <summary>
/// Background hosted service that drives the transactional outbox pattern.
///
/// Polls the outbox table every <paramref name="pollingInterval"/> for Pending messages,
/// publishes each one to MassTransit, then marks it Processed or Failed.
/// Uses a shared ResiliencePipeline (retry + circuit breaker) per message.
///
/// The outbox table is cleaned up in the same loop — processed rows older than
/// <paramref name="retentionPeriod"/> are deleted in a single batch.
/// </summary>
public class OutboxProcessor : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OutboxProcessor> _logger;
    private readonly ResiliencePipeline _pipeline;
    private readonly TimeSpan _pollingInterval;
    private readonly TimeSpan _retentionPeriod;
    private readonly int _batchSize;

    public OutboxProcessor(
        IServiceScopeFactory scopeFactory,
        ILogger<OutboxProcessor> logger,
        TimeSpan? pollingInterval = null,
        TimeSpan? retentionPeriod = null,
        int batchSize = 100)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _pollingInterval = pollingInterval ?? TimeSpan.FromSeconds(5);
        _retentionPeriod = retentionPeriod ?? TimeSpan.FromDays(7);
        _batchSize = batchSize;
        _pipeline = BuildPipeline();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "OutboxProcessor starting — polling every {Interval}s, batch={Batch}, retention={Retention}d",
            _pollingInterval.TotalSeconds, _batchSize, _retentionPeriod.TotalDays);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingMessagesAsync(stoppingToken);
                await CleanupProcessedAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OutboxProcessor: unexpected error during processing loop");
            }

            await Task.Delay(_pollingInterval, stoppingToken);
        }

        _logger.LogInformation("OutboxProcessor stopped");
    }

    private async Task ProcessPendingMessagesAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BaseDbContext>();
        var outboxRepo = scope.ServiceProvider.GetRequiredService<IOutboxRepository>();
        var publishEndpoint = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();

        // Fetch one batch of pending messages
        var messages = await outboxRepo.GetPendingAsync(_batchSize, ct);
        if (messages.Count == 0) return;

        _logger.LogDebug("OutboxProcessor: {Count} pending messages to process", messages.Count);

        foreach (var message in messages)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                await _pipeline.ExecuteAsync(async token =>
                {
                    var @event = DeserializePayload(message.Type, message.Payload);
                    await publishEndpoint.Publish(@event, token);
                }, ct);

                message.MarkAsProcessed();
                await db.SaveChangesAsync(ct);

                _logger.LogDebug("OutboxProcessor: published {Type}, Id={Id}", message.Type, message.Id);
            }
            catch (BrokenCircuitException)
            {
                // Circuit breaker is open — stop processing this batch and wait for the next poll
                _logger.LogWarning(
                    "OutboxProcessor: circuit breaker OPEN, backing off. Message {Id} left pending", message.Id);
                break;
            }
            catch (Exception ex)
            {
                message.MarkAsFailed();
                await db.SaveChangesAsync(ct);

                _logger.LogError(ex,
                    "OutboxProcessor: failed to publish message {Id} (Type={Type}) after all retries — marked as Failed",
                    message.Id, message.Type);
            }
        }
    }

    private async Task CleanupProcessedAsync(CancellationToken ct)
    {
        var threshold = DateTime.UtcNow - _retentionPeriod;
        using var scope = _scopeFactory.CreateScope();
        var outboxRepo = scope.ServiceProvider.GetRequiredService<IOutboxRepository>();
        await outboxRepo.DeleteProcessedOlderThanAsync(threshold, ct);
    }

    /// <summary>
    /// Deserializes the JSON payload into the original event object using the
    /// assembly-qualified type name stored in <see cref="OutboxMessage.Type"/>.
    /// </summary>
    private static object? DeserializePayload(string typeName, string payload)
    {
        var type = Type.GetType(typeName)
            ?? throw new InvalidOperationException($"OutboxProcessor: could not resolve type '{typeName}'");

        return System.Text.Json.JsonSerializer.Deserialize(payload, type);
    }

    private static ResiliencePipeline BuildPipeline()
    {
        return new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromMilliseconds(300),
                BackoffType = DelayBackoffType.Exponential,
                ShouldHandle = new PredicateBuilder()
                    .Handle<Exception>(ex => ex is not OperationCanceledException),
            })
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                FailureRatio = 0.3,
                MinimumThroughput = 5,
                SamplingDuration = TimeSpan.FromSeconds(30),
                BreakDuration = TimeSpan.FromSeconds(30),
                ShouldHandle = new PredicateBuilder()
                    .Handle<Exception>(ex => ex is not OperationCanceledException),
            })
            .Build();
    }
}
