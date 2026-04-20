namespace Common.Application.Interfaces;

/// <summary>
/// Weighted multi-factor scoring engine for cross-border opportunities.
/// </summary>
public interface IScoringEngine
{
    /// <summary>
    /// Calculate composite score (0–100).
    /// </summary>
    decimal CalculateCompositeScore(
        decimal profitMarginPct,
        decimal demandScore,
        decimal competitionScore,
        decimal priceStabilityScore,
        decimal matchConfidenceScore,
        Dictionary<string, decimal>? customWeights = null);

    /// <summary>
    /// Normalise a raw value to a 0–100 scale given a min/max range.
    /// </summary>
    decimal Normalize(decimal value, decimal min, decimal max);
}
