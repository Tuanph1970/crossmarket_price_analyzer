using Common.Domain.Entities;
using Common.Domain.Enums;

namespace MatchingService.Domain.Entities;

/// <summary>
/// Aggregate root: represents a US ↔ Vietnam product pairing.
/// </summary>
public class ProductMatch : AuditableEntity<Guid>
{
    public Guid UsProductId { get; set; }
    public Guid VnProductId { get; set; }
    public decimal ConfidenceScore { get; set; } // 0-100
    public MatchStatus Status { get; set; } = MatchStatus.Pending;
    public string? ConfirmedBy { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public ICollection<MatchConfirmation> Confirmations { get; set; } = new List<MatchConfirmation>();

    public ConfidenceLevel GetConfidenceLevel() => ConfidenceScore switch
    {
        >= 80 => ConfidenceLevel.High,
        >= 60 => ConfidenceLevel.Medium,
        _ => ConfidenceLevel.Low
    };

    /// <summary>
    /// Factory method — creates a new ProductMatch with auto-generated Id.
    /// Use this from any layer where direct entity construction with Id is needed.
    /// </summary>
    public static ProductMatch Create(
        Guid usProductId,
        Guid vnProductId,
        decimal confidenceScore,
        MatchStatus status = MatchStatus.Pending)
    {
        return new ProductMatch
        {
            Id = Guid.NewGuid(),
            UsProductId = usProductId,
            VnProductId = vnProductId,
            ConfidenceScore = confidenceScore,
            Status = status,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Confirm(string userId, string? notes = null)
    {
        if (Status != MatchStatus.Pending)
            throw new InvalidOperationException($"Cannot confirm match in status {Status}");

        Status = MatchStatus.Confirmed;
        ConfirmedBy = userId;
        ConfirmedAt = DateTime.UtcNow;
        var matchId = Id;
        Confirmations.Add(MatchConfirmation.Create(matchId, userId, ConfirmAction.Confirmed, notes));
    }

    public void Reject(string userId, string? notes = null)
    {
        if (Status != MatchStatus.Pending)
            throw new InvalidOperationException($"Cannot reject match in status {Status}");

        Status = MatchStatus.Rejected;
        ConfirmedBy = userId;
        ConfirmedAt = DateTime.UtcNow;
        var matchId = Id;
        Confirmations.Add(MatchConfirmation.Create(matchId, userId, ConfirmAction.Rejected, notes));
    }
}