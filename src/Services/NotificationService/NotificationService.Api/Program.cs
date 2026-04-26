using Common.Application.Extensions;
using Common.Infrastructure.Configuration;
using MassTransit;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NotificationService.Infrastructure.Consumers;
using NotificationService.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseCommonLogging();
builder.Services.AddCommonInfrastructure(builder.Configuration, "NotificationService");
builder.Services.AddCommonApplication();

// P4-B04: REST API — add DbContext
builder.Services.AddDbContext<NotificationDbContext>(options =>
{
    var cs = builder.Configuration.GetConnectionString("DefaultConnection");
    var serverVersion = new MySqlServerVersion(new Version(8, 0, 0));
    options.UseMySql(cs, serverVersion);
});

// P4-B05: SendGrid email service
builder.Services.AddHttpClient<IEmailNotificationService, EmailNotificationService>();

// P4-B06: Telegram bot service
builder.Services.AddHttpClient<ITelegramNotificationService, TelegramNotificationService>(client =>
{
    var botToken = builder.Configuration["Telegram:BotToken"];
    client.BaseAddress = new Uri($"https://api.telegram.org/bot{botToken ?? "mock"}");
});

// P4-B08: Report generator (QuestPDF)
builder.Services.AddScoped<IReportGeneratorService, ReportGeneratorService>();

// P4-B07: Alert threshold engine
builder.Services.AddHostedService<AlertThresholdEngine>();

// P4-B09: Scheduled report worker
builder.Services.AddHostedService<ScheduledReportWorker>();

// P4-B07: Wire MassTransit consumer for OpportunityScoredEvent
// Note: AddCommonInfrastructure already calls AddMassTransit() for the service bus.
// We only need to add the consumer endpoint here — the MassTransit bus itself
// was already configured by AddCommonInfrastructure.

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "NotificationService API", Version = "v1" });
    var xmlFile = $"{typeof(Program).Assembly.GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
        c.IncludeXmlComments(xmlPath);
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
    await db.Database.EnsureCreatedAsync();
}

app.UseSwagger();
app.UseSwaggerUI();

// ── Health ──────────────────────────────────────────────────────────────────────
app.MapGet("/health", () => Results.Ok(new { Status = "Healthy", Service = "NotificationService", Timestamp = DateTime.UtcNow }))
   .WithTags("Health")
   .WithName("HealthCheck")
   .WithDescription("Returns the health status of the NotificationService.");

// ── Alerts (delivery log feed) ───────────────────────────────────────────────

app.MapGet("/api/alerts", async (
    [FromQuery] Guid userId,
    [FromQuery] int page,
    [FromQuery] int pageSize,
    NotificationDbContext db,
    CancellationToken ct) =>
{
    if (page < 1) page = 1;
    if (pageSize < 1 || pageSize > 100) pageSize = 20;

    var query = db.DeliveryLogs
        .Where(d => d.UserId == userId && d.Success)
        .OrderByDescending(d => d.SentAt);

    var total = await query.CountAsync(ct);
    var items = await query
        .Skip((page - 1) * pageSize)
        .Take(pageSize)
        .Select(d => new AlertDto(d.Id, d.MessageContent, d.MatchId, d.SentAt, d.IsRead, d.Channel.ToString()))
        .ToListAsync(ct);

    return Results.Ok(new { items, total, page, pageSize });
})
.Produces(StatusCodes.Status200OK)
.WithTags("Alerts")
.WithName("GetAlerts");

app.MapPut("/api/alerts/{id:guid}/read", async (
    Guid id,
    NotificationDbContext db,
    CancellationToken ct) =>
{
    var log = await db.DeliveryLogs.FindAsync([id], ct);
    if (log is null) return Results.NotFound();
    log.MarkRead();
    await db.SaveChangesAsync(ct);
    return Results.NoContent();
})
.WithTags("Alerts")
.WithName("MarkAlertRead");

app.MapDelete("/api/alerts/{id:guid}", async (
    Guid id,
    NotificationDbContext db,
    CancellationToken ct) =>
{
    var log = await db.DeliveryLogs.FindAsync([id], ct);
    if (log is null) return Results.NotFound();
    db.DeliveryLogs.Remove(log);
    await db.SaveChangesAsync(ct);
    return Results.NoContent();
})
.WithTags("Alerts")
.WithName("DeleteAlert");

// ── P4-B05: Email template preview ─────────────────────────────────────────
app.MapGet("/api/notifications/email/preview", (
    string userName,
    string productName,
    decimal score,
    decimal margin,
    string matchUrl,
    IEmailNotificationService svc) =>
{
    var html = svc.BuildOpportunityAlertHtml(userName, productName, score, margin, matchUrl);
    return Results.Content(html, "text/html");
})
.WithTags("Notifications")
.WithName("PreviewEmailTemplate")
.WithDescription("Returns a preview of the opportunity alert HTML email template.");

// ── P4-B08: Report generation ────────────────────────────────────────────────
app.MapPost("/api/notifications/reports/opportunity", async (
    GenerateReportRequest req,
    IReportGeneratorService reportService,
    CancellationToken ct) =>
{
    var bytes = req.Format.ToLowerInvariant() == "csv"
        ? await reportService.GenerateOpportunityCsvAsync(
            new OpportunityReportRequest(
                req.Title, req.Format,
                req.Opportunities.Select(o => new OpportunitySummaryDto(
                    o.MatchId, o.UsProductName, o.VnProductName,
                    o.CompositeScore, o.ProfitMarginPct,
                    o.DemandScore, o.CompetitionScore,
                    o.PriceStabilityScore, o.MatchConfidenceScore,
                    o.LandedCostVnd, o.VietnamRetailVnd)).ToList(),
                req.PeriodFrom, req.PeriodTo, req.UserId), ct)
        : await reportService.GenerateOpportunityReportAsync(
            new OpportunityReportRequest(
                req.Title, req.Format,
                req.Opportunities.Select(o => new OpportunitySummaryDto(
                    o.MatchId, o.UsProductName, o.VnProductName,
                    o.CompositeScore, o.ProfitMarginPct,
                    o.DemandScore, o.CompetitionScore,
                    o.PriceStabilityScore, o.MatchConfidenceScore,
                    o.LandedCostVnd, o.VietnamRetailVnd)).ToList(),
                req.PeriodFrom, req.PeriodTo, req.UserId), ct);

    var contentType = req.Format.ToLowerInvariant() == "csv"
        ? "text/csv"
        : "application/pdf";
    var ext = req.Format.ToLowerInvariant() == "csv" ? "csv" : "pdf";
    var filename = $"cma-report-{DateTime.UtcNow:yyyyMMdd-HHmmss}.{ext}";

    return Results.File(bytes, contentType, filename);
})
.Produces(StatusCodes.Status200OK)
.WithTags("Reports")
.WithName("GenerateOpportunityReport")
.WithDescription("Generates an opportunity report in PDF or CSV format.");

// ── P4-B06: Telegram digest preview ──────────────────────────────────────────
app.MapPost("/api/notifications/telegram/preview", (
    AlertDigestRequest req,
    ITelegramNotificationService svc) =>
{
    var items = req.Items.Select(i =>
        new AlertDigestItem(i.ProductName, i.CompositeScore, i.ProfitMarginPct)).ToList();
    var markdown = svc.BuildDigestMarkdown(items, req.TotalMatches);
    return Results.Ok(new { Preview = markdown });
})
.WithTags("Notifications")
.WithName("PreviewTelegramDigest")
.WithDescription("Returns a preview of the Telegram daily digest message.");

app.Run();

// ── Request DTOs ────────────────────────────────────────────────────────────────

public record GenerateReportRequest(
    string Title,
    string Format, // "pdf" | "csv"
    List<OpportunityRefDto> Opportunities,
    DateTime PeriodFrom,
    DateTime PeriodTo,
    Guid? UserId = null
);

public record OpportunityRefDto(
    Guid MatchId,
    string UsProductName,
    string VnProductName,
    decimal CompositeScore,
    decimal ProfitMarginPct,
    decimal DemandScore,
    decimal CompetitionScore,
    decimal PriceStabilityScore,
    decimal MatchConfidenceScore,
    decimal LandedCostVnd,
    decimal VietnamRetailVnd
);

public record AlertDigestRequest(
    IReadOnlyList<AlertDigestItemDto> Items,
    int TotalMatches
);

public record AlertDigestItemDto(
    string ProductName,
    decimal CompositeScore,
    decimal ProfitMarginPct
);

public record AlertDto(
    Guid Id,
    string Message,
    Guid? MatchId,
    DateTime SentAt,
    bool IsRead,
    string Channel
);
