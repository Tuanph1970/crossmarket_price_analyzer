using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NotificationService.Infrastructure.Services;

namespace NotificationService.Infrastructure.Services;

/// <summary>
/// P4-B07: Alert threshold engine.
/// Background service that evaluates alert thresholds on each OpportunityScoredEvent
/// and dispatches notifications via Email/Telegram/InApp channels.
/// </summary>
public sealed class AlertThresholdEngine : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IEmailNotificationService _emailService;
    private readonly ITelegramNotificationService _telegramService;
    private readonly ILogger<AlertThresholdEngine> _logger;

    public AlertThresholdEngine(
        IServiceScopeFactory scopeFactory,
        IEmailNotificationService emailService,
        ITelegramNotificationService telegramService,
        ILogger<AlertThresholdEngine> logger)
    {
        _scopeFactory = scopeFactory;
        _emailService = emailService;
        _telegramService = telegramService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("AlertThresholdEngine started — running every 60 seconds");

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(60));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await EvaluateThresholdsAsync(stoppingToken);
        }
    }

    /// <summary>
    /// Evaluates all active alert thresholds against current scores.
    /// Called by MassTransit consumer on OpportunityScoredEvent (real-time path)
    /// and by this timer's periodic check (batch path).
    /// </summary>
    public async Task EvaluateThresholdsAsync(
        OpportunityScoredEvent evt,
        CancellationToken ct = default)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();

            var thresholds = await db.AlertPreferences
                .Where(p => p.IsEnabled)
                .Where(p => p.MinScoreThreshold <= evt.CompositeScore)
                .Where(p => !p.MinMarginThreshold.HasValue ||
                           p.MinMarginThreshold <= evt.ProfitMarginPct)
                .ToListAsync(ct);

            foreach (var threshold in thresholds)
            {
                await DispatchAlertAsync(threshold, evt, ct);
            }

            _logger.LogDebug(
                "Evaluated {Count} thresholds for match {MatchId}: {Matched} triggered",
                thresholds.Count, evt.MatchId,
                thresholds.Count(t => t.MinScoreThreshold <= evt.CompositeScore));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AlertThresholdEngine failed for match {MatchId}", evt.MatchId);
        }
    }

    private async Task EvaluateThresholdsAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            // TODO: In v2, call ScoringService to get current top opportunities
            // and batch-evaluate against all active thresholds
            _logger.LogDebug("AlertThresholdEngine: periodic evaluation cycle");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AlertThresholdEngine periodic evaluation failed");
        }
    }

    private async Task DispatchAlertAsync(
        Domain.Entities.AlertPreference threshold,
        OpportunityScoredEvent evt,
        CancellationToken ct)
    {
        var matchUrl = $"{_scopeFactory.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>()["App:BaseUrl"]}/compare/{evt.MatchId}";

        switch (threshold.Channel)
        {
            case Common.Domain.Enums.DeliveryChannel.Email:
                var html = _emailService.BuildOpportunityAlertHtml(
                    "User", evt.ProductName, evt.CompositeScore, evt.ProfitMarginPct, matchUrl);
                await _emailService.SendAlertEmailAsync(
                    threshold.DeliveryTarget, "CrossMarket Opportunity Alert", html, ct);
                break;

            case Common.Domain.Enums.DeliveryChannel.Telegram:
                var markdown = _telegramService.BuildOpportunityAlertMarkdown(
                    evt.ProductName, evt.CompositeScore, evt.ProfitMarginPct, matchUrl);
                await _telegramService.SendTelegramMessageAsync(
                    threshold.DeliveryTarget, markdown, ct);
                break;

            case Common.Domain.Enums.DeliveryChannel.InApp:
                // TODO (v2): push to SignalR hub or Redis pub/sub for in-app notifications
                _logger.LogInformation("InApp alert for user {UserId}: match {MatchId} score {Score}",
                    threshold.UserId, evt.MatchId, evt.CompositeScore);
                break;
        }

        // Log delivery
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
        var log = Domain.Entities.DeliveryLog.Create(
            threshold.UserId, threshold.Channel, threshold.DeliveryTarget,
            $"Opportunity alert: {evt.ProductName} (Score {evt.CompositeScore:F0})",
            success: true, matchId: evt.MatchId);
        db.DeliveryLogs.Add(log);
        await db.SaveChangesAsync(ct);
    }
}

/// <summary>
/// Event published by ScoringService when a new composite score is calculated.
/// Consumed by AlertThresholdEngine to trigger real-time notifications.
/// </summary>
public sealed record OpportunityScoredEvent(
    Guid MatchId,
    string ProductName,
    decimal CompositeScore,
    decimal ProfitMarginPct
);