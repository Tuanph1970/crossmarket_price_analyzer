using Common.Application.Extensions;
using Common.Infrastructure.Configuration;
using MatchingService.Application.Commands;
using MatchingService.Application.DTOs;
using MatchingService.Application.Persistence;
using MatchingService.Application.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Infrastructure + Swagger
builder.Host.UseCommonLogging();
builder.Services.AddCommonInfrastructure(builder.Configuration, "MatchingService");
builder.Services.AddCommonApplication();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c => c.SwaggerDoc("v1", new() { Title = "MatchingService API", Version = "v1" }));

// Database
builder.Services.AddDbContext<MatchingDbContext>(options =>
{
    var cs = builder.Configuration.GetConnectionString("DefaultConnection");
    options.UseMySql(cs, ServerVersion.AutoDetect(cs));
});

// Application services
builder.Services.AddScoped<ProductMatchRepository>();
builder.Services.AddScoped<FuzzyMatchingService>();

// MediatR
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<CreateMatchCommand>());

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<MatchingDbContext>().Database.EnsureCreatedAsync();
}

app.UseSwagger();
app.UseSwaggerUI();

// GET /api/matches
app.MapGet("/api/matches", async (
    ProductMatchRepository repo,
    int page = 1,
    int pageSize = 20,
    string? status = null,
    decimal? minScore = null,
    CancellationToken ct = default) =>
{
    Common.Domain.Enums.MatchStatus? statusFilter = null;
    if (!string.IsNullOrEmpty(status) && Enum.TryParse<Common.Domain.Enums.MatchStatus>(status, true, out var s))
        statusFilter = s;

    var (items, total) = await repo.GetPaginatedAsync(page, pageSize, statusFilter, minScore, ct);
    var dtos = items.Select(m => new ProductMatchDto(
        m.Id, m.UsProductId, m.VnProductId, m.ConfidenceScore,
        m.GetConfidenceLevel(), m.Status, m.ConfirmedBy, m.ConfirmedAt, m.CreatedAt,
        m.Confirmations?.Select(c => new MatchConfirmationDto(
            c.Id, c.MatchId, c.UserId, c.Action.ToString(), c.Notes
        )).ToList()
    )).ToList();

    return Results.Ok(new PaginatedMatchesDto(dtos, total, page, pageSize,
        (int)Math.Ceiling(total / (double)pageSize)));
});

// GET /api/matches/{id}
app.MapGet("/api/matches/{id:guid}", async (Guid id, ProductMatchRepository repo, CancellationToken ct) =>
{
    var m = await repo.GetByIdAsync(id, ct);
    if (m is null) return Results.NotFound();
    return Results.Ok(new ProductMatchDto(
        m.Id, m.UsProductId, m.VnProductId, m.ConfidenceScore,
        m.GetConfidenceLevel(), m.Status, m.ConfirmedBy, m.ConfirmedAt, m.CreatedAt,
        m.Confirmations?.Select(c => new MatchConfirmationDto(
            c.Id, c.MatchId, c.UserId, c.Action.ToString(), c.Notes
        )).ToList()
    ));
});

// POST /api/matches
app.MapPost("/api/matches", async (
    CreateMatchRequest req,
    FuzzyMatchingService fuzzyService,
    ProductMatchRepository repo,
    CancellationToken ct) =>
{
    var score = fuzzyService.ComputeMatchScore(
        req.UsProductName, req.VnProductName, req.UsBrand, req.VnBrand);

    var match = MatchingService.Domain.Entities.ProductMatch.Create(
        req.UsProductId, req.VnProductId, score);

    await repo.AddAsync(match, ct);

    return Results.Created($"/api/matches/{match.Id}", new ProductMatchDto(
        match.Id, match.UsProductId, match.VnProductId, match.ConfidenceScore,
        match.GetConfidenceLevel(), match.Status, match.ConfirmedBy,
        match.ConfirmedAt, match.CreatedAt, null
    ));
});

// POST /api/matches/{id}/confirm
app.MapPost("/api/matches/{id:guid}/confirm", async (
    Guid id,
    ConfirmMatchRequest req,
    ProductMatchRepository repo,
    CancellationToken ct) =>
{
    var m = await repo.GetByIdAsync(id, ct);
    if (m is null) return Results.NotFound();
    m.Confirm(req.UserId ?? "system", req.Notes);
    await repo.UpdateAsync(m, ct);
    return Results.Ok(new { Status = "Confirmed", MatchId = m.Id });
});

// POST /api/matches/{id}/reject
app.MapPost("/api/matches/{id:guid}/reject", async (
    Guid id,
    RejectMatchRequest req,
    ProductMatchRepository repo,
    CancellationToken ct) =>
{
    var m = await repo.GetByIdAsync(id, ct);
    if (m is null) return Results.NotFound();
    m.Reject(req.UserId ?? "system", req.Notes);
    await repo.UpdateAsync(m, ct);
    return Results.Ok(new { Status = "Rejected", MatchId = m.Id });
});

// POST /api/matches/batch-review
app.MapPost("/api/matches/batch-review", async (
    BatchReviewRequest req,
    string? userId = null,
    ProductMatchRepository? repo = null,
    CancellationToken ct = default) =>
{
    var processed = 0;
    var user = userId ?? "system";
    foreach (var item in req.Items)
    {
        var m = await repo!.GetByIdAsync(item.MatchId, ct);
        if (m is null || m.Status != Common.Domain.Enums.MatchStatus.Pending) continue;

        if (item.Action.Equals("Confirm", StringComparison.OrdinalIgnoreCase))
            m.Confirm(user);
        else if (item.Action.Equals("Reject", StringComparison.OrdinalIgnoreCase))
            m.Reject(user);
        else continue;

        await repo.UpdateAsync(m, ct);
        processed++;
    }
    return Results.Ok(new { Processed = processed });
});

app.MapGet("/health", () => Results.Ok(new { Status = "Healthy", Service = "MatchingService", Timestamp = DateTime.UtcNow }));

app.Run();

// Request DTOs
public record ConfirmMatchRequest(string? UserId = null, string? Notes = null);
public record RejectMatchRequest(string? UserId = null, string? Notes = null);
