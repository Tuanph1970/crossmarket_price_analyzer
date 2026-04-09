using Common.Domain.Entities;

namespace ScoringService.Domain.Entities;

/// <summary>
/// Aggregate root: represents the calculated opportunity score for a product match.
/// </summary>
public class OpportunityScore : AuditableEntity<Guid>
{
    public Guid MatchId { get; set; }

    // Individual factor scores (raw, before normalization)
    public decimal ProfitMarginPct { get; set; }
    public decimal DemandScore { get; set; }       // 0-100 (higher = more demand)
    public decimal CompetitionScore { get; set; }   // 0-100 (higher = more competition = worse)
    public decimal PriceStabilityScore { get; set; } // 0-100
    public decimal MatchConfidenceScore { get; set; } // 0-100

    // Composite weighted score
    public decimal CompositeScore { get; set; }      // 0-100

    // Financials (all in VND)
    public decimal LandedCostVnd { get; set; }
    public decimal VietnamRetailVnd { get; set; }
    public decimal PriceDifferenceVnd { get; set; }

    // Computed metrics
    public decimal ProfitMargin => VietnamRetailVnd > 0
        ? (PriceDifferenceVnd / VietnamRetailVnd) * 100m
        : 0;
    public decimal Roi => LandedCostVnd > 0
        ? (PriceDifferenceVnd / LandedCostVnd) * 100m
        : 0;

    public DateTime CalculatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Factory method — creates a new OpportunityScore with auto-generated Id.
    /// Use this from Application layer where entity constructors are not available.
    /// </summary>
    public static OpportunityScore Create(
        Guid matchId,
        decimal profitMarginPct,
        decimal demandScore,
        decimal competitionScore,
        decimal priceStabilityScore,
        decimal matchConfidenceScore,
        decimal compositeScore,
        decimal landedCostVnd,
        decimal vietnamRetailVnd)
    {
        return new OpportunityScore
        {
            Id = Guid.NewGuid(),
            MatchId = matchId,
            ProfitMarginPct = profitMarginPct,
            DemandScore = demandScore,
            CompetitionScore = competitionScore,
            PriceStabilityScore = priceStabilityScore,
            MatchConfidenceScore = matchConfidenceScore,
            CompositeScore = compositeScore,
            LandedCostVnd = landedCostVnd,
            VietnamRetailVnd = vietnamRetailVnd,
            PriceDifferenceVnd = vietnamRetailVnd - landedCostVnd,
            CalculatedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }
}
