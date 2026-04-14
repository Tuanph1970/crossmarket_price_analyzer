using Common.Domain.Enums;

namespace NotificationService.Domain.Entities;

/// <summary>
/// User alert preference for opportunity notifications.
/// </summary>
public sealed class AlertPreference : Common.Domain.Entities.BaseEntity<Guid>
{
    public Guid UserId { get; private set; }
    public DeliveryChannel Channel { get; private set; }
    public string DeliveryTarget { get; private set; } = string.Empty; // email address or Telegram chat ID
    public decimal MinScoreThreshold { get; private set; } = 50m;
    public decimal? MinMarginThreshold { get; private set; }
    public bool IsEnabled { get; private set; } = true;
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    private AlertPreference() { } // EF Core

    public static AlertPreference Create(
        Guid userId,
        DeliveryChannel channel,
        string deliveryTarget,
        decimal minScoreThreshold = 50m,
        decimal? minMarginThreshold = null)
    {
        return new AlertPreference
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Channel = channel,
            DeliveryTarget = deliveryTarget,
            MinScoreThreshold = minScoreThreshold,
            MinMarginThreshold = minMarginThreshold,
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow,
        };
    }

    public void Disable() { IsEnabled = false; UpdatedAt = DateTime.UtcNow; }
    public void Enable() { IsEnabled = true; UpdatedAt = DateTime.UtcNow; }
    public void UpdateThresholds(decimal minScore, decimal? minMargin)
    {
        MinScoreThreshold = minScore;
        MinMarginThreshold = minMargin;
        UpdatedAt = DateTime.UtcNow;
    }
}
