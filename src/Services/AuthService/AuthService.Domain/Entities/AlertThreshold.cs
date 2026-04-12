using Common.Domain.Entities;

namespace AuthService.Domain.Entities;

/// <summary>
/// A user-specific alert threshold for a product match.
/// Triggers email/Telegram/in-app notification when score crosses the threshold.
/// </summary>
public sealed class AlertThreshold : AuditableEntity<Guid>
{
    public Guid UserId { get; private set; }
    public Guid? MatchId { get; private set; }  // null = all matches
    public string Name { get; private set; } = string.Empty;

    /// <summary>Score must be >= this value to trigger.</summary>
    public decimal MinScore { get; private set; }
    public decimal? MaxScore { get; private set; }

    /// <summary>Minimum profit margin % to trigger.</summary>
    public decimal? MinMarginPct { get; private set; }

    /// <summary>Whether this threshold is active.</summary>
    public bool IsActive { get; private set; } = true;

    /// <summary>Minimum score change delta to avoid alert spam (e.g. 5 = score must move by 5 pts).</summary>
    public decimal MinScoreDelta { get; private set; } = 5m;

    public static AlertThreshold Create(
        Guid userId,
        string name,
        decimal minScore,
        decimal? maxScore = null,
        decimal? minMarginPct = null,
        Guid? matchId = null)
    {
        if (minScore < 0 || minScore > 100)
            throw new ArgumentOutOfRangeException(nameof(minScore), "minScore must be between 0 and 100");

        return new AlertThreshold
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            MatchId = matchId,
            Name = name.Trim(),
            MinScore = minScore,
            MaxScore = maxScore,
            MinMarginPct = minMarginPct,
            IsActive = true,
        };
    }

    public void Deactivate() => IsActive = false;
    public void Reactivate() => IsActive = true;
    public void UpdateThresholds(decimal minScore, decimal? maxScore, decimal? minMarginPct)
    {
        if (minScore < 0 || minScore > 100)
            throw new ArgumentOutOfRangeException(nameof(minScore));
        MinScore = minScore;
        MaxScore = maxScore;
        MinMarginPct = minMarginPct;
    }
}