namespace NotificationService.Domain.Entities;

/// <summary>
/// Log of each notification delivery attempt.
/// </summary>
public sealed class DeliveryLog : Common.Domain.Entities.BaseEntity<Guid>
{
    public Guid UserId { get; private set; }
    public Common.Domain.Enums.DeliveryChannel Channel { get; private set; }
    public string DeliveryTarget { get; private set; } = string.Empty;
    public string MessageContent { get; private set; } = string.Empty;
    public bool Success { get; private set; }
    public string? ErrorMessage { get; private set; }
    public DateTime SentAt { get; private set; }
    public Guid? MatchId { get; private set; }
    public bool IsRead { get; private set; }

    public void MarkRead() => IsRead = true;

    private DeliveryLog() { } // EF Core

    public static DeliveryLog Create(
        Guid userId,
        Common.Domain.Enums.DeliveryChannel channel,
        string deliveryTarget,
        string messageContent,
        bool success,
        Guid? matchId = null,
        string? errorMessage = null)
    {
        return new DeliveryLog
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Channel = channel,
            DeliveryTarget = deliveryTarget,
            MessageContent = messageContent,
            Success = success,
            ErrorMessage = errorMessage,
            SentAt = DateTime.UtcNow,
            MatchId = matchId,
        };
    }
}