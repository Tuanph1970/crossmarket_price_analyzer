using Microsoft.EntityFrameworkCore;
using ScoringService.Application.DTOs;
using ScoringService.Application.Queries;
using ScoringService.Domain.Entities;
using ScoringService.Application.Persistence;
using ScoringService.Application.Services;

namespace ScoringService.Application.Handlers;

public class GetOpportunitiesHandler : MediatR.IRequestHandler<GetOpportunitiesQuery, PaginatedScoresDto>
{
    private readonly ScoringDbContext _db;

    public GetOpportunitiesHandler(ScoringDbContext db)
    {
        _db = db;
    }

    public async Task<PaginatedScoresDto> Handle(GetOpportunitiesQuery query, CancellationToken ct)
    {
        var dbQuery = _db.OpportunityScores.AsQueryable();

        if (query.MinMargin.HasValue)
            dbQuery = dbQuery.Where(s => s.ProfitMarginPct >= query.MinMargin.Value);

        var total = await dbQuery.CountAsync(ct);
        var items = await dbQuery
            .OrderByDescending(s => s.CompositeScore)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(ct);

        var dtos = items.Select(s => new OpportunityScoreDto(
            s.Id, s.MatchId, s.CompositeScore, s.ProfitMarginPct,
            s.VietnamRetailVnd > 0 ? (s.PriceDifferenceVnd / s.LandedCostVnd) * 100m : 0,
            s.DemandScore, s.CompetitionScore, s.PriceStabilityScore,
            s.MatchConfidenceScore, s.LandedCostVnd, s.VietnamRetailVnd,
            s.PriceDifferenceVnd, s.CalculatedAt
        )).ToList();

        return new PaginatedScoresDto(
            dtos, total, query.Page, query.PageSize,
            (int)Math.Ceiling(total / (double)query.PageSize)
        );
    }
}

public class GetScoreByMatchIdHandler : MediatR.IRequestHandler<GetScoreByMatchIdQuery, ScoringBreakdownDto?>
{
    private readonly ScoringDbContext _db;
    private readonly ScoringEngine _scoringEngine;

    public GetScoreByMatchIdHandler(ScoringDbContext db, ScoringEngine scoringEngine)
    {
        _db = db;
        _scoringEngine = scoringEngine;
    }

    public async Task<ScoringBreakdownDto?> Handle(GetScoreByMatchIdQuery query, CancellationToken ct)
    {
        var s = await _db.OpportunityScores
            .FirstOrDefaultAsync(x => x.MatchId == query.MatchId, ct);

        if (s is null) return null;

        var weights = ScoringEngine.DefaultWeights;
        var competitionAdjusted = 100m - Math.Clamp(s.CompetitionScore, 0m, 100m);

        var factors = new List<FactorScoreDto>
        {
            new("ProfitMargin", s.ProfitMarginPct, weights["ProfitMargin"],
                _scoringEngine.Normalize(s.ProfitMarginPct, 0m, 50m),
                _scoringEngine.Normalize(s.ProfitMarginPct, 0m, 50m) * weights["ProfitMargin"] / 100m),
            new("Demand", s.DemandScore, weights["Demand"],
                Math.Clamp(s.DemandScore, 0m, 100m),
                Math.Clamp(s.DemandScore, 0m, 100m) * weights["Demand"] / 100m),
            new("Competition", s.CompetitionScore, weights["Competition"],
                competitionAdjusted,
                competitionAdjusted * weights["Competition"] / 100m),
            new("Stability", s.PriceStabilityScore, weights["Stability"],
                Math.Clamp(s.PriceStabilityScore, 0m, 100m),
                Math.Clamp(s.PriceStabilityScore, 0m, 100m) * weights["Stability"] / 100m),
            new("Confidence", s.MatchConfidenceScore, weights["Confidence"],
                Math.Clamp(s.MatchConfidenceScore, 0m, 100m),
                Math.Clamp(s.MatchConfidenceScore, 0m, 100m) * weights["Confidence"] / 100m),
        };

        return new ScoringBreakdownDto(
            s.MatchId, s.CompositeScore, factors,
            new LandedCostBreakdownDto(
                s.LandedCostVnd * 0.95m, // Approximate breakdown
                s.LandedCostVnd * 0.05m, // Simplified
                s.LandedCostVnd * 0.05m,
                s.LandedCostVnd * 0.10m,
                s.LandedCostVnd * 0.02m,
                s.LandedCostVnd
            )
        );
    }
}

public class GetScoringConfigHandler : MediatR.IRequestHandler<GetScoringConfigQuery, ScoringConfigDto>
{
    public Task<ScoringConfigDto> Handle(GetScoringConfigQuery request, CancellationToken ct)
    {
        var weights = ScoringEngine.DefaultWeights
            .Select(kv => new ScoringConfigItemDto(kv.Key, kv.Value, 0m, 100m))
            .ToList();

        return Task.FromResult(new ScoringConfigDto(weights));
    }
}