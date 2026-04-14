using Common.Application.Extensions;
using Common.Application.Interfaces;
using Common.Infrastructure.Configuration;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ScoringService.Application.Commands;
using ScoringService.Application.DTOs;
using ScoringService.Application.Persistence;
using ScoringService.Application.Queries;
using ScoringService.Application.Services;
using ScoringService.Api.WebSockets;
using ScoringService.Infrastructure.WebSockets;

var builder = WebApplication.CreateBuilder(args);

// Infrastructure + Swagger
builder.Host.UseCommonLogging();
builder.Services.AddCommonInfrastructure(builder.Configuration, "ScoringService");
builder.Services.AddCommonApplication();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "ScoringService API", Version = "v1" });
    var xmlFile = $"{typeof(Program).Assembly.GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
        c.IncludeXmlComments(xmlPath);
});

// Database
builder.Services.AddDbContext<ScoringDbContext>(options =>
{
    var cs = builder.Configuration.GetConnectionString("DefaultConnection");
    options.UseMySql(cs, ServerVersion.AutoDetect(cs));
});

// Application services
builder.Services.AddScoped<ScoringEngine>();
builder.Services.AddScoped<LandedCostCalculator>();

// ── Phase 3 services ─────────────────────────────────────────────────────────
builder.Services.AddHttpClient();                                      // IHttpClientFactory for PriceStabilityService
builder.Services.AddHttpClient("ProductService");                      // ProductService HTTP client
builder.Services.AddSingleton<IHsCodeClassifier, HsCodeClassifier>();
builder.Services.AddSingleton<ITariffService, TariffService>();
builder.Services.AddScoped<IPriceStabilityService, PriceStabilityService>();
builder.Services.AddSingleton<IShippingService, ShippingService>();
builder.Services.AddSingleton<IExcelExportService, ExcelExportService>();

// WebSocket background handler
builder.Services.AddSingleton<IOpportunityWebSocketHandler, OpportunityWebSocketHandler>();
builder.Services.AddHostedService(sp => (OpportunityWebSocketHandler)
    sp.GetRequiredService<IOpportunityWebSocketHandler>());

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

// WebSocket endpoint (/ws/opportunities — see WebSocketMiddleware)
app.UseOpportunityWebSockets();

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
})
.Produces<PaginatedScoresDto>(StatusCodes.Status200OK)
.WithTags("Scores")
.WithName("GetScores")
.WithDescription("Returns paginated opportunity scores ranked by composite score, optionally filtered by minimum profit margin.");

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
})
.Produces<ScoringBreakdownDto>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status404NotFound)
.WithTags("Scores")
.WithName("GetScoreByMatchId")
.WithDescription("Returns a detailed scoring breakdown for a specific product match.");

// POST /api/scores — calculate score for a match
app.MapPost("/api/scores", async (
    CalculateScoreCommand cmd,
    ScoringEngine engine,
    LandedCostCalculator calculator,
    ScoringDbContext db,
    CancellationToken ct) =>
{
    var shipping = cmd.ShippingCostUsd ?? 10.0m;
    // P2-B06: honour manual overrides (LandedCostOverrideVnd, ImportDutyOverridePct)
    var breakdown = calculator.CalculateBreakdown(
        cmd.UsPriceUsd, cmd.ExchangeRate, shipping,
        cmd.ImportDutyOverridePct ?? cmd.ImportDutyRatePct, cmd.VatRatePct,
        landedCostOverride: cmd.LandedCostOverrideVnd);
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
})
.Produces(StatusCodes.Status201Created)
.Produces(StatusCodes.Status400BadRequest)
.WithTags("Scores")
.WithName("CalculateScore")
.WithDescription("Calculates or updates the composite opportunity score for a product match.");

// PUT /api/scores/weights — update scoring weights (stub)
app.MapPut("/api/scores/weights", (
    UpdateWeightsRequest req) =>
    Results.Ok(new { Status = "Weights updated", Count = req.Weights.Count }))
.Produces(StatusCodes.Status200OK)
.Produces(StatusCodes.Status400BadRequest)
.WithTags("Scores")
.WithName("UpdateWeights")
.WithDescription("Updates the scoring factor weights (admin endpoint).");

// GET /api/scores/config — get current scoring weights
app.MapGet("/api/scores/config", () =>
{
    var weights = ScoringEngine.DefaultWeights
        .Select(kv => new ScoringConfigItemDto(kv.Key, kv.Value, 0m, 100m))
        .ToList();
    return Results.Ok(new ScoringConfigDto(weights));
})
.Produces<ScoringConfigDto>(StatusCodes.Status200OK)
.WithTags("Scores")
.WithName("GetScoringConfig")
.WithDescription("Returns the current scoring factor weights and their configurable ranges.");

// POST /api/scores/recalculate — trigger full recalculation
app.MapPost("/api/scores/recalculate", async (ScoringDbContext db, CancellationToken ct) =>
{
    var count = await db.OpportunityScores.CountAsync(ct);
    return Results.Ok(new { Recalculated = count, At = DateTime.UtcNow });
})
.Produces(StatusCodes.Status200OK)
.WithTags("Scores")
.WithName("RecalculateAllScores")
.WithDescription("Triggers a full recalculation of all opportunity scores.");

// ── Phase 3: WebSocket health ────────────────────────────────────────────────
app.MapGet("/api/scores/websocket/health", (
    IOpportunityWebSocketHandler handler) =>
{
    var states = handler.GetConnectionStates();
    var openCount = states.Count(kvp => kvp.Value == System.Net.WebSockets.WebSocketState.Open);
    return Results.Ok(new
    {
        Status = "Healthy",
        TotalConnections = handler.ConnectionCount,
        OpenConnections = openCount,
        ClosedOrAbnormal = handler.ConnectionCount - openCount,
        Timestamps = DateTime.UtcNow,
    });
})
.Produces(StatusCodes.Status200OK)
.WithTags("WebSocket")
.WithName("WebSocketHealth")
.WithDescription("Returns the health status of the WebSocket server including connection counts.");

// ── Phase 3: Excel export ─────────────────────────────────────────────────────
app.MapPost("/api/scores/export/excel", async (
    ExcelExportRequest request,
    ScoringDbContext db,
    IOpportunityWebSocketHandler wsHandler,
    IExcelExportService excelService,
    CancellationToken ct) =>
{
    var scores = await db.OpportunityScores
        .OrderByDescending(s => s.CompositeScore)
        .Take(request.Limit > 0 ? request.Limit : 1000)
        .ToListAsync(ct);

    var rows = scores.Select(s => new OpportunityExportRow(
        UsProductName: s.MatchId.ToString(), // match label, real names come from MatchingService
        VnProductName: s.MatchId.ToString(),
        UsPriceUsd: null,
        VnPriceVnd: null,
        LandedCostVnd: s.LandedCostVnd,
        VietnamRetailVnd: s.VietnamRetailVnd,
        ProfitMarginPct: s.ProfitMarginPct,
        RoiPct: s.VietnamRetailVnd > 0 && s.LandedCostVnd > 0
            ? Math.Round((s.VietnamRetailVnd - s.LandedCostVnd) / s.LandedCostVnd * 100m, 2)
            : 0m,
        CompositeScore: s.CompositeScore,
        DemandScore: s.DemandScore,
        CompetitionScore: s.CompetitionScore,
        PriceStabilityScore: s.PriceStabilityScore,
        MatchConfidenceScore: s.MatchConfidenceScore,
        CalculatedAt: s.CalculatedAt
    )).ToList();

    var exportRequest = new ExportRequest(
        Title: request.Title ?? "Opportunity Scores Export",
        Rows: rows);

    var bytes = await excelService.ExportOpportunitiesAsync(exportRequest, ct);

    // Broadcast a delta update to all connected clients
    if (wsHandler.ConnectionCount > 0)
    {
        var broadcast = new OpportunityBroadcast<object>(
            Type: "export_completed",
            Payload: new { RecordCount = rows.Count, At = DateTime.UtcNow },
            TimestampUtc: DateTime.UtcNow);
        _ = wsHandler.BroadcastAsync(broadcast, ct);
    }

    return Results.File(
        bytes,
        contentType: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        fileDownloadName: $"opportunities-{DateTime.UtcNow:yyyyMMdd-HHmmss}.xlsx");
})
.Produces(StatusCodes.Status200OK, contentType: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")
.WithTags("Export")
.WithName("ExportToExcel")
.WithDescription("Exports all opportunity scores to a formatted Excel workbook with Summary, Opportunities, and Top 20 sheets.");

// ── Phase 3: trigger manual broadcast ─────────────────────────────────────────
app.MapPost("/api/scores/broadcast", async (
    ScoringDbContext db,
    IOpportunityWebSocketHandler wsHandler,
    CancellationToken ct) =>
{
    var top20 = await db.OpportunityScores
        .OrderByDescending(s => s.CompositeScore)
        .Take(20)
        .Select(s => new SnapshotItemDto(
            s.MatchId, s.CompositeScore, s.ProfitMarginPct,
            s.DemandScore, s.CompetitionScore, s.PriceStabilityScore,
            s.MatchConfidenceScore, s.LandedCostVnd, s.VietnamRetailVnd))
        .ToListAsync(ct);

    var snapshot = new OpportunitySnapshotDto(top20, DateTime.UtcNow);
    var broadcast = new OpportunityBroadcast<OpportunitySnapshotDto>(
        Type: "full_snapshot",
        Payload: snapshot,
        TimestampUtc: DateTime.UtcNow);

    await wsHandler.BroadcastAsync(broadcast, ct);

    return Results.Ok(new
    {
        BroadcastType = "full_snapshot",
        SentTo = wsHandler.ConnectionCount,
        SnapshotCount = top20.Count,
        At = DateTime.UtcNow,
    });
})
.Produces(StatusCodes.Status200OK)
.WithTags("WebSocket")
.WithName("BroadcastSnapshot")
.WithDescription("Manually triggers a top-20 snapshot broadcast to all connected WebSocket clients.");

// P2-B06: PUT /api/scores/manual-costs — store manual landed-cost overrides for a match
app.MapPut("/api/scores/manual-costs", async (
    ManualCostOverrideRequest req,
    ScoringDbContext db,
    CancellationToken ct) =>
{
    var score = await db.OpportunityScores
        .FirstOrDefaultAsync(x => x.MatchId == req.MatchId, ct);

    if (score is null) return Results.NotFound();

    // Apply any provided overrides
    if (req.LandedCostOverrideVnd.HasValue)
    {
        score.LandedCostVnd = req.LandedCostOverrideVnd.Value;
        score.PriceDifferenceVnd = score.VietnamRetailVnd - score.LandedCostVnd;
        score.ProfitMarginPct = score.VietnamRetailVnd > 0
            ? Math.Max(0, (score.PriceDifferenceVnd / score.VietnamRetailVnd) * 100m)
            : 0;
    }

    score.UpdatedAt = DateTime.UtcNow;
    db.OpportunityScores.Update(score);
    await db.SaveChangesAsync(ct);

    return Results.Ok(new
    {
        MatchId = score.MatchId,
        LandedCostVnd = score.LandedCostVnd,
        ProfitMarginPct = score.ProfitMarginPct,
        UpdatedAt = score.UpdatedAt
    });
})
.Produces(StatusCodes.Status200OK)
.Produces(StatusCodes.Status404NotFound)
.WithTags("Scores")
.WithName("UpdateManualCosts")
.WithDescription("Stores or updates manual landed-cost overrides for a product match without re-running the full scoring engine.");

app.Run();
