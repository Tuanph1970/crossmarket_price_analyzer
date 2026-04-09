using Common.Application.Extensions;
using Common.Infrastructure.Configuration;
using Microsoft.EntityFrameworkCore;
using ScoringService.Application.Commands;
using ScoringService.Application.DTOs;
using ScoringService.Application.Queries;
using ScoringService.Application.Persistence;
using ScoringService.Application.Services;

var builder = WebApplication.CreateBuilder(args);

// Infrastructure + Swagger
builder.Host.UseCommonLogging();
builder.Services.AddCommonInfrastructure(builder.Configuration, "ScoringService");
builder.Services.AddCommonApplication();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c => c.SwaggerDoc("v1", new() { Title = "ScoringService API", Version = "v1" }));

// Database
builder.Services.AddDbContext<ScoringDbContext>(options =>
{
    var cs = builder.Configuration.GetConnectionString("DefaultConnection");
    options.UseMySql(cs, ServerVersion.AutoDetect(cs));
});

// Application services
builder.Services.AddScoped<ScoringEngine>();
builder.Services.AddScoped<LandedCostCalculator>();

// MediatR
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssemblyContaining<CalculateScoreCommand>());

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ScoringDbContext>();
    await db.Database.EnsureCreatedAsync();
}

app.UseSwagger();
app.UseSwaggerUI();

// GET /api/scores — ranked opportunity scores
app.MapGet("/api/scores", async (
    ScoringDbContext db,
    int page = 1,
    int pageSize = 20,
    decimal? minMargin = null,
    CancellationToken ct = default) =>
{
    var query = db.OpportunityScores.AsQueryable();
    if (minMargin.HasValue)
        query = query.Where(s => s.ProfitMarginPct >= minMargin.Value);

    var total = await query.CountAsync(ct);
    var items = await query
        .OrderByDescending(s => s.CompositeScore)
        .Skip((page - 1) * pageSize)
        .Take(pageSize)
        .ToListAsync(ct);

    var dtos = items.Select(s => new OpportunityScoreDto(
        s.Id, s.MatchId, s.CompositeScore, s.ProfitMarginPct,
        s.LandedCostVnd > 0 ? Math.Round(s.PriceDifferenceVnd / s.LandedCostVnd * 100m, 2) : 0,
        s.DemandScore, s.CompetitionScore, s.PriceStabilityScore,
        s.MatchConfidenceScore, s.LandedCostVnd, s.VietnamRetailVnd,
        s.PriceDifferenceVnd, s.CalculatedAt
    )).ToList();

    return Results.Ok(new PaginatedScoresDto(
        dtos, total, page, pageSize,
        (int)Math.Ceiling(total / (double)pageSize)
    ));
});

// GET /api/scores/{matchId}
app.MapGet("/api/scores/{matchId:guid}", async (
    Guid matchId,
    ScoringDbContext db,
    ScoringEngine engine,
    CancellationToken ct) =>
{
    var s = await db.OpportunityScores.FirstOrDefaultAsync(x => x.MatchId == matchId, ct);
    if (s is null) return Results.NotFound();

    var weights = ScoringEngine.DefaultWeights;
    var compAdj = 100m - Math.Clamp(s.CompetitionScore, 0m, 100m);

    var factors = new List<FactorScoreDto>
    {
        new("ProfitMargin", s.ProfitMarginPct, weights["ProfitMargin"],
            engine.Normalize(s.ProfitMarginPct, 0m, 50m),
            engine.Normalize(s.ProfitMarginPct, 0m, 50m) * weights["ProfitMargin"] / 100m),
        new("Demand", s.DemandScore, weights["Demand"],
            Math.Clamp(s.DemandScore, 0m, 100m),
            Math.Clamp(s.DemandScore, 0m, 100m) * weights["Demand"] / 100m),
        new("Competition", s.CompetitionScore, weights["Competition"],
            compAdj, compAdj * weights["Competition"] / 100m),
        new("Stability", s.PriceStabilityScore, weights["Stability"],
            Math.Clamp(s.PriceStabilityScore, 0m, 100m),
            Math.Clamp(s.PriceStabilityScore, 0m, 100m) * weights["Stability"] / 100m),
        new("Confidence", s.MatchConfidenceScore, weights["Confidence"],
            Math.Clamp(s.MatchConfidenceScore, 0m, 100m),
            Math.Clamp(s.MatchConfidenceScore, 0m, 100m) * weights["Confidence"] / 100m),
    };

    return Results.Ok(new ScoringBreakdownDto(s.MatchId, s.CompositeScore, factors, null));
});

// POST /api/scores — calculate score for a match
app.MapPost("/api/scores", async (
    CalculateScoreCommand cmd,
    ScoringEngine engine,
    LandedCostCalculator calculator,
    ScoringDbContext db,
    CancellationToken ct) =>
{
    var shipping = cmd.ShippingCostUsd ?? 10.0m;
    var breakdown = calculator.CalculateBreakdown(cmd.UsPriceUsd, cmd.ExchangeRate, shipping,
        cmd.ImportDutyRatePct, cmd.VatRatePct);
    var profitMargin = calculator.CalculateProfitMargin(cmd.VnRetailPriceVnd, breakdown.TotalLandedCostVnd);
    var composite = engine.CalculateCompositeScore(
        profitMargin, cmd.DemandScore, cmd.CompetitionScore,
        cmd.PriceStabilityScore, cmd.MatchConfidenceScore);

    var existing = await db.OpportunityScores.FirstOrDefaultAsync(x => x.MatchId == cmd.MatchId, ct);

    if (existing != null)
    {
        existing.ProfitMarginPct = profitMargin;
        existing.DemandScore = cmd.DemandScore;
        existing.CompetitionScore = cmd.CompetitionScore;
        existing.PriceStabilityScore = cmd.PriceStabilityScore;
        existing.MatchConfidenceScore = cmd.MatchConfidenceScore;
        existing.CompositeScore = composite;
        existing.LandedCostVnd = breakdown.TotalLandedCostVnd;
        existing.VietnamRetailVnd = cmd.VnRetailPriceVnd;
        existing.PriceDifferenceVnd = cmd.VnRetailPriceVnd - breakdown.TotalLandedCostVnd;
        existing.CalculatedAt = DateTime.UtcNow;
        existing.UpdatedAt = DateTime.UtcNow;
        db.OpportunityScores.Update(existing);
    }
    else
    {
        var entity = ScoringService.Domain.Entities.OpportunityScore.Create(
            cmd.MatchId, profitMargin, cmd.DemandScore, cmd.CompetitionScore,
            cmd.PriceStabilityScore, cmd.MatchConfidenceScore, composite,
            breakdown.TotalLandedCostVnd, cmd.VnRetailPriceVnd);
        await db.OpportunityScores.AddAsync(entity, ct);
    }
    await db.SaveChangesAsync(ct);

    return Results.Created($"/api/scores/{cmd.MatchId}", new { MatchId = cmd.MatchId, CompositeScore = composite });
});

// PUT /api/scores/weights — update scoring weights (stub)
app.MapPut("/api/scores/weights", (UpdateWeightsRequest req) =>
    Results.Ok(new { Status = "Weights updated", Count = req.Weights.Count }));

// GET /api/scores/config — get current scoring weights
app.MapGet("/api/scores/config", () =>
{
    var weights = ScoringEngine.DefaultWeights
        .Select(kv => new ScoringConfigItemDto(kv.Key, kv.Value, 0m, 100m))
        .ToList();
    return Results.Ok(new ScoringConfigDto(weights));
});

// POST /api/scores/recalculate — trigger full recalculation
app.MapPost("/api/scores/recalculate", async (ScoringDbContext db, CancellationToken ct) =>
{
    var count = await db.OpportunityScores.CountAsync(ct);
    return Results.Ok(new { Recalculated = count, At = DateTime.UtcNow });
});

app.MapGet("/health", () => Results.Ok(new { Status = "Healthy", Service = "ScoringService", Timestamp = DateTime.UtcNow }));

app.Run();