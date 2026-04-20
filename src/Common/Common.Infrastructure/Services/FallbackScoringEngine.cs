using Common.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Common.Infrastructure.Services;

/// <summary>
/// Stub scoring engine used by ProductService when ScoringService is unavailable.
/// Uses default weights — real scoring is done by ScoringService.
/// </summary>
public sealed class FallbackScoringEngine : IScoringEngine
{
    private readonly ILogger<FallbackScoringEngine> _logger;

    public static readonly Dictionary<string, decimal> DefaultWeights = new()
    {
        { "ProfitMargin", 40m },
        { "Demand", 25m },
        { "Competition", 20m },
        { "Stability", 10m },
        { "Confidence", 5m }
    };

    public FallbackScoringEngine(ILogger<FallbackScoringEngine> logger)
        => _logger = logger;

    public decimal CalculateCompositeScore(
        decimal profitMarginPct,
        decimal demandScore,
        decimal competitionScore,
        decimal priceStabilityScore,
        decimal matchConfidenceScore,
        Dictionary<string, decimal>? customWeights = null)
    {
        var weights = customWeights ?? DefaultWeights;
        var totalWeight = weights.Values.Sum();
        if (totalWeight == 0) return 0;

        var normalizedProfit = Normalize(profitMarginPct, 0m, 50m);
        var competitionAdjusted = 100m - Math.Clamp(competitionScore, 0m, 100m);

        return Math.Round(
            normalizedProfit * (weights["ProfitMargin"] / totalWeight) +
            Math.Clamp(demandScore, 0m, 100m) * (weights["Demand"] / totalWeight) +
            competitionAdjusted * (weights["Competition"] / totalWeight) +
            Math.Clamp(priceStabilityScore, 0m, 100m) * (weights["Stability"] / totalWeight) +
            Math.Clamp(matchConfidenceScore, 0m, 100m) * (weights["Confidence"] / totalWeight),
            2);
    }

    public decimal Normalize(decimal value, decimal min, decimal max)
    {
        if (max == min) return 50m;
        return Math.Clamp((value - min) / (max - min) * 100m, 0m, 100m);
    }
}
