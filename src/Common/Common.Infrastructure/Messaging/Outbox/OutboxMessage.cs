using Common.Domain.Entities;

namespace Common.Infrastructure.Messaging.Outbox;

/// <summary>
/// Outbox message entity for reliable event delivery.
/// Messages are written to the outbox table in the same transaction as the
/// domain entity change, then processed asynchronously by OutboxProcessor.
/// </summary>
public class OutboxMessage : BaseEntity<Guid>
{
    /// <summary>Full assembly-qualified event type name.</summary>
    public string Type { get; protected internal set; } = string.Empty;

    /// <summary>JSON-serialized event payload.</summary>
    public string Payload { get; protected internal set; } = string.Empty;

    public DateTime CreatedAt { get; protected internal set; }

    /// <summary>UTC timestamp when the message was successfully processed. Null if pending/failed.</summary>
    public DateTime? ProcessedAt { get; protected internal set; }

    public OutboxMessageStatus Status { get; protected internal set; } = OutboxMessageStatus.Pending;

    // EF Core constructor — do not use directly, use factory methods
    public OutboxMessage() { }

    /// <summary>
    /// Factory: creates a new pending OutboxMessage.
    /// </summary>
    public static OutboxMessage Create(string type, string payload)
    {
        return new OutboxMessage
        {
            Id = Guid.NewGuid(),
            Type = type,
            Payload = payload,
            CreatedAt = DateTime.UtcNow,
            Status = OutboxMessageStatus.Pending,
            ProcessedAt = null
        };
    }

    public void MarkAsProcessed()
    {
        Status = OutboxMessageStatus.Processed;
        ProcessedAt = DateTime.UtcNow;
    }

    public void MarkAsFailed()
    {
        Status = OutboxMessageStatus.Failed;
    }
}

public enum OutboxMessageStatus
{
    Pending,
    Processed,
    Failed
}
