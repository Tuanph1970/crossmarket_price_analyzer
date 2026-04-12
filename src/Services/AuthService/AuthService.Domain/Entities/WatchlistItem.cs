using Common.Domain.Entities;

namespace AuthService.Domain.Entities;

/// <summary>
/// A product being watched by a user.
/// Multi-tenancy: UserId is the tenant key.
/// </summary>
public sealed class WatchlistItem : AuditableEntity<Guid>
{
    public Guid UserId { get; private set; }
    public Guid ProductMatchId { get; private set; }

    // Display helpers
    public string UsProductName { get; private set; } = string.Empty;
    public string VnProductName { get; private set; } = string.Empty;
    public decimal? AlertAboveScore { get; private set; }
    public decimal? AlertBelowScore { get; private set; }

    public bool IsMuted { get; private set; }

    public static WatchlistItem Create(
        Guid userId,
        Guid productMatchId,
        string usProductName,
        string vnProductName,
        decimal? alertAboveScore = null,
        decimal? alertBelowScore = null)
    {
        return new WatchlistItem
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ProductMatchId = productMatchId,
            UsProductName = usProductName,
            VnProductName = vnProductName,
            AlertAboveScore = alertAboveScore,
            AlertBelowScore = alertBelowScore,
            IsMuted = false,
        };
    }

    public void UpdateAlertThresholds(decimal? above, decimal? below)
    {
        AlertAboveScore = above;
        AlertBelowScore = below;
    }

    public void Mute() => IsMuted = true;
    public void Unmute() => IsMuted = false;
    public void Remove() { /* soft-delete via base entity if needed */ }
}