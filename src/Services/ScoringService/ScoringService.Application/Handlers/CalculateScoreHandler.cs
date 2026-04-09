using Microsoft.EntityFrameworkCore;
using ScoringService.Application.Commands;
using ScoringService.Application.DTOs;
using ScoringService.Application.Persistence;
using ScoringService.Application.Services;
using ScoringService.Domain.Entities;

namespace ScoringService.Application.Handlers;

public class CalculateScoreHandler : MediatR.IRequestHandler<CalculateScoreCommand, ScoringBreakdownDto>
{
    private readonly ScoringDbContext _db;
    private readonly ScoringEngine _engine;
    private readonly LandedCostCalculator _calc;

    public CalculateScoreHandler(ScoringDbContext db, ScoringEngine engine, LandedCostCalculator calc)
    {
        _db = db;
        _engine = engine;
        _calc = calc;
    }

    public async Task<ScoringBreakdownDto> Handle(CalculateScoreCommand cmd, CancellationToken ct)
    {
        // 1. Landed cost
        var shipping = cmd.ShippingCostUsd ?? 10m;
        var breakdown = _calc.CalculateBreakdown(
            cmd.UsPriceUsd, cmd.ExchangeRate, shipping,
            cmd.ImportDutyRatePct, cmd.VatRatePct);
        var landedCost = breakdown.TotalLandedCostVnd;
        var margin = _calc.CalculateProfitMargin(cmd.VnRetailPriceVnd, landedCost);
        var composite = _engine.CalculateCompositeScore(
            margin, cmd.DemandScore, cmd.CompetitionScore,
            cmd.PriceStabilityScore, cmd.MatchConfidenceScore);

        // 2. Factor breakdown
        var compAdj = 100m - Math.Clamp(cmd.CompetitionScore, 0m, 100m);
        var factors = new List<FactorScoreDto> {
            new("ProfitMargin", margin, 40m,
                _engine.Normalize(margin, 0m, 50m),
                _engine.Normalize(margin, 0m, 50m) * 0.4m),
            new("Demand", cmd.DemandScore, 25m,
                Math.Clamp(cmd.DemandScore, 0m, 100m),
                Math.Clamp(cmd.DemandScore, 0m, 100m) * 0.25m),
            new("Competition", cmd.CompetitionScore, 20m,
                compAdj, compAdj * 0.2m),
            new("Stability", cmd.PriceStabilityScore, 10m,
                Math.Clamp(cmd.PriceStabilityScore, 0m, 100m),
                Math.Clamp(cmd.PriceStabilityScore, 0m, 100m) * 0.1m),
            new("Confidence", cmd.MatchConfidenceScore, 5m,
                Math.Clamp(cmd.MatchConfidenceScore, 0m, 100m),
                Math.Clamp(cmd.MatchConfidenceScore, 0m, 100m) * 0.05m),
        };

        // 3. Save: check if existing
        var existing = await _db.OpportunityScores.FirstOrDefaultAsync(
            x => x.MatchId == cmd.MatchId, ct);

        if (existing != null)
        {
            // Update existing entity (Id is accessible on already-fetched tracked entity)
            existing.ProfitMarginPct = margin;
            existing.DemandScore = cmd.DemandScore;
            existing.CompetitionScore = cmd.CompetitionScore;
            existing.PriceStabilityScore = cmd.PriceStabilityScore;
            existing.MatchConfidenceScore = cmd.MatchConfidenceScore;
            existing.CompositeScore = composite;
            existing.LandedCostVnd = landedCost;
            existing.VietnamRetailVnd = cmd.VnRetailPriceVnd;
            existing.PriceDifferenceVnd = cmd.VnRetailPriceVnd - landedCost;
            existing.CalculatedAt = DateTime.UtcNow;
            existing.UpdatedAt = DateTime.UtcNow;
            _db.OpportunityScores.Update(existing);
        }
        else
        {
            // Create new — use factory method to avoid Id setter access
            var newScore = OpportunityScore.Create(
                cmd.MatchId, margin, cmd.DemandScore, cmd.CompetitionScore,
                cmd.PriceStabilityScore, cmd.MatchConfidenceScore,
                composite, landedCost, cmd.VnRetailPriceVnd);
            await _db.OpportunityScores.AddAsync(newScore, ct);
        }

        await _db.SaveChangesAsync(ct);

        return new ScoringBreakdownDto(
            cmd.MatchId, composite, factors,
            new LandedCostBreakdownDto(
                breakdown.UsPurchasePriceVnd, breakdown.ShippingCostVnd,
                breakdown.ImportDutyVnd, breakdown.VatVnd,
                breakdown.HandlingFeesVnd, breakdown.TotalLandedCostVnd));
    }
}

public class RecalculateAllScoresHandler : MediatR.IRequestHandler<RecalculateAllScoresCommand, int>
{
    private readonly ScoringDbContext _db;
    public RecalculateAllScoresHandler(ScoringDbContext db) => _db = db;
    public Task<int> Handle(RecalculateAllScoresCommand req, CancellationToken ct)
        => Task.FromResult(_db.OpportunityScores.Local.Count);
}
