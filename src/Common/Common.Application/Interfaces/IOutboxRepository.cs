using Common.Infrastructure.Messaging.Outbox;

namespace Common.Application.Interfaces;

/// <summary>
/// Repository contract for outbox message persistence.
/// Defined in Common.Application so it can be referenced by both Application and Infrastructure
/// without creating a circular dependency.
/// </summary>
public interface IOutboxRepository
{
    /// <summary>Adds a new message to the outbox.</summary>
    Task AddAsync(OutboxMessage message, CancellationToken ct = default);

    /// <summary>
    /// Retrieves a page of pending messages ordered by creation time (oldest first),
    /// scoped to a batch size so the processor doesn't re-acquire them all at once.
    /// </summary>
    Task<IReadOnlyList<OutboxMessage>> GetPendingAsync(int batchSize, CancellationToken ct = default);

    /// <summary>Marks a message as processed (ProcessedAt set, Status = Processed).</summary>
    Task MarkAsProcessedAsync(OutboxMessage message, CancellationToken ct = default);

    /// <summary>Marks a message as permanently failed (Status = Failed).</summary>
    Task MarkAsFailedAsync(OutboxMessage message, CancellationToken ct = default);

    /// <summary>
    /// Deletes processed messages older than <paramref name="threshold"/>.
    /// Used by a scheduled cleanup job to keep the outbox table lean.
    /// </summary>
    Task DeleteProcessedOlderThanAsync(DateTime threshold, CancellationToken ct = default);
}
