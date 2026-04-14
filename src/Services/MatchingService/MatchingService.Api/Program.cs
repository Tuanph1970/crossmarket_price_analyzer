using Common.Application.Extensions;
using Common.Infrastructure.Configuration;
using MatchingService.Application.Commands;
using MatchingService.Application.DTOs;
using MatchingService.Application.Persistence;
using MatchingService.Application.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Infrastructure + Swagger
builder.Host.UseCommonLogging();
builder.Services.AddCommonInfrastructure(builder.Configuration, "MatchingService");
builder.Services.AddCommonApplication();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "MatchingService API", Version = "v1" });
    var xmlFile = $"{typeof(Program).Assembly.GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
        c.IncludeXmlComments(xmlPath);
});

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
})
.Produces<PaginatedMatchesDto>(StatusCodes.Status200OK)
.WithTags("Matches")
.WithName("GetMatches")
.WithDescription("Returns paginated product matches with optional filtering by status and minimum confidence score.");

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
})
.Produces<ProductMatchDto>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status404NotFound)
.WithTags("Matches")
.WithName("GetMatchById")
.WithDescription("Returns a single product match by its unique identifier.");

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
})
.Produces<ProductMatchDto>(StatusCodes.Status201Created)
.ProducesValidationProblem()
.WithTags("Matches")
.WithName("CreateMatch")
.WithDescription("Creates a new US↔Vietnam product match and computes its fuzzy confidence score.");

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
})
.Produces(StatusCodes.Status200OK)
.Produces(StatusCodes.Status404NotFound)
.WithTags("Matches")
.WithName("ConfirmMatch")
.WithDescription("Confirms a product match, marking it as verified.");

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
})
.Produces(StatusCodes.Status200OK)
.Produces(StatusCodes.Status404NotFound)
.WithTags("Matches")
.WithName("RejectMatch")
.WithDescription("Rejects a product match, marking it as invalid.");

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
})
.Produces(StatusCodes.Status200OK)
.ProducesValidationProblem()
.WithTags("Matches")
.WithName("BatchReviewMatches")
.WithDescription("Performs a batch confirm/reject operation on multiple pending matches.");

app.MapGet("/health", () => Results.Ok(new { Status = "Healthy", Service = "MatchingService", Timestamp = DateTime.UtcNow }))
   .WithTags("Health")
   .WithName("HealthCheck")
   .WithDescription("Returns the health status of the MatchingService.");

app.Run();

/// <summary>
/// Request to create a product match.
/// </summary>
/// <param name="UsProductId">Identifier of the US product.</param>
/// <param name="VnProductId">Identifier of the Vietnamese product.</param>
/// <param name="UsProductName">US product name (used for fuzzy scoring).</param>
/// <param name="VnProductName">Vietnamese product name (used for fuzzy scoring).</param>
/// <param name="UsBrand">US product brand (optional, used for fuzzy scoring).</param>
/// <param name="VnBrand">Vietnamese product brand (optional, used for fuzzy scoring).</param>
public record CreateMatchRequest(
    Guid UsProductId,
    Guid VnProductId,
    string UsProductName,
    string VnProductName,
    string? UsBrand = null,
    string? VnBrand = null
);

/// <summary>
/// Request to confirm a product match.
/// </summary>
/// <param name="UserId">Identifier of the user performing the confirmation.</param>
/// <param name="Notes">Optional notes explaining the confirmation.</param>
public record ConfirmMatchRequest(string? UserId = null, string? Notes = null);

/// <summary>
/// Request to reject a product match.
/// </summary>
/// <param name="UserId">Identifier of the user performing the rejection.</param>
/// <param name="Notes">Optional notes explaining the rejection.</param>
public record RejectMatchRequest(string? UserId = null, string? Notes = null);

/// <summary>
/// Request for batch review of multiple product matches.
/// </summary>
/// <param name="Items">List of match IDs and actions to apply.</param>
public record BatchReviewRequest(List<BatchReviewItem> Items);

/// <summary>
/// A single item in a batch review request.
/// </summary>
/// <param name="MatchId">Identifier of the match to review.</param>
/// <param name="Action">Action to apply: "Confirm" or "Reject".</param>
public record BatchReviewItem(Guid MatchId, string Action);

public partial class Program { }
