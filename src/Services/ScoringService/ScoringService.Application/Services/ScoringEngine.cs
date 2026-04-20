using Common.Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using ScoringService.Application.Persistence;
using ScoringService.Domain.Entities;

namespace ScoringService.Application.Services;

/// <summary>
/// Weighted multi-factor scoring engine for cross-border opportunities.
/// </summary>
public sealed class ScoringEngine : IScoringEngine
{
    public static readonly Dictionary<string, decimal> DefaultWeights = new()
    {
        { "ProfitMargin", 40m },
        { "Demand", 25m },
        { "Competition", 20m },
        { "Stability", 10m },
        { "Confidence", 5m }
    };

    /// <summary>
    /// Calculate composite score (0-100).
    /// </summary>
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

/// <summary>
/// Repository for OpportunityScore entities.
/// </summary>
public class OpportunityScoreRepository
{
    private readonly ScoringDbContext _db;

    public OpportunityScoreRepository(ScoringDbContext db)
    {
        _db = db;
    }

    public async Task<OpportunityScore?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _db.OpportunityScores.FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<OpportunityScore?> GetByMatchIdAsync(Guid matchId, CancellationToken ct = default)
        => await _db.OpportunityScores.FirstOrDefaultAsync(x => x.MatchId == matchId, ct);

    public async Task<IReadOnlyList<OpportunityScore>> GetTopOpportunitiesAsync(
        int count, int skip = 0, CancellationToken ct = default)
        => await _db.OpportunityScores
            .AsNoTracking()
            .OrderByDescending(x => x.CompositeScore)
            .Skip(skip).Take(count).ToListAsync(ct);

    public async Task<(IReadOnlyList<OpportunityScore> Items, int Total)> GetRankedOpportunitiesAsync(
        int page, int pageSize,
        decimal? minMargin = null,
        CancellationToken ct = default)
    {
        var query = _db.OpportunityScores.AsQueryable();
        if (minMargin.HasValue)
            query = query.Where(x => x.ProfitMarginPct >= minMargin.Value);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(x => x.CompositeScore)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .AsNoTracking().ToListAsync(ct);

        return (items, total);
    }

    public async Task SaveAsync(OpportunityScore score, CancellationToken ct = default)
    {
        var existing = await _db.OpportunityScores
            .FirstOrDefaultAsync(x => x.MatchId == score.MatchId, ct);

        if (existing != null)
        {
            existing.ProfitMarginPct = score.ProfitMarginPct;
            existing.DemandScore = score.DemandScore;
            existing.CompetitionScore = score.CompetitionScore;
            existing.PriceStabilityScore = score.PriceStabilityScore;
            existing.MatchConfidenceScore = score.MatchConfidenceScore;
            existing.CompositeScore = score.CompositeScore;
            existing.LandedCostVnd = score.LandedCostVnd;
            existing.VietnamRetailVnd = score.VietnamRetailVnd;
            existing.PriceDifferenceVnd = score.PriceDifferenceVnd;
            existing.CalculatedAt = DateTime.UtcNow;
            existing.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            var newScore = OpportunityScore.Create(
                score.MatchId, score.ProfitMarginPct, score.DemandScore,
                score.CompetitionScore, score.PriceStabilityScore,
                score.MatchConfidenceScore, score.CompositeScore,
                score.LandedCostVnd, score.VietnamRetailVnd);
            await _db.OpportunityScores.AddAsync(newScore, ct);
        }
        await _db.SaveChangesAsync(ct);
    }
}
