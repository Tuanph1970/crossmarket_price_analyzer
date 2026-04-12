using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace NotificationService.Infrastructure.Services;

/// <summary>
/// P4-B08: Scheduled report worker.
/// Background service that runs daily/weekly/monthly and generates
/// opportunity reports for users with scheduled subscriptions.
/// TODO (v2): Replace Quartz.NET placeholder with real Quartz.NET scheduler.
/// </summary>
public sealed class ScheduledReportWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ScheduledReportWorker> _logger;

    public ScheduledReportWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<ScheduledReportWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ScheduledReportWorker started — checking at midnight and midday");

        // Two runs per day: 00:00 UTC (daily digest) and 12:00 UTC (midday summary)
        while (!stoppingToken.IsCancellationRequested)
        {
            var now = DateTime.UtcNow;
            var nextRun = GetNextRun(now);
            var delay = nextRun - now;

            if (delay > TimeSpan.Zero)
            {
                try
                {
                    await Task.Delay(delay, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }

            await RunScheduledReportsAsync(stoppingToken);
        }
    }

    private static DateTime GetNextRun(DateTime now)
    {
        // Next midnight
        var midnight = now.Date.AddDays(1);
        // Next midday
        var midday = now.Date.AddHours(12);

        if (midday > now) return midday;
        return midnight;
    }

    private async Task RunScheduledReportsAsync(CancellationToken ct)
    {
        _logger.LogInformation("ScheduledReportWorker: running scheduled reports...");

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();

            var dueReports = db.ScheduledReports
                .Where(r => r.IsActive)
                .Where(r => r.NextRunAt == null || r.NextRunAt <= DateTime.UtcNow)
                .ToList();

            foreach (var report in dueReports)
            {
                try
                {
                    // TODO (v2): Call ScoringService for latest scores, then generate PDF/CSV
                    var now = DateTime.UtcNow;
                    var periodFrom = report.Schedule switch
                    {
                        "daily" => now.AddDays(-1),
                        "weekly" => now.AddDays(-7),
                        "monthly" => now.AddMonths(-1),
                        _ => now.AddDays(-1),
                    };

                    report.MarkRun(now, GetNextRunFromSchedule(report.Schedule, now));
                    await db.SaveChangesAsync(ct);

                    _logger.LogInformation(
                        "Scheduled report '{Name}' ran for user {UserId} (format: {Format})",
                        report.ReportName, report.UserId, report.Format);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Scheduled report '{Name}' failed for user {UserId}",
                        report.ReportName, report.UserId);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ScheduledReportWorker batch run failed");
        }
    }

    private static DateTime GetNextRunFromSchedule(string schedule, DateTime from)
    {
        return schedule switch
        {
            "daily" => from.Date.AddDays(1).AddHours(0),
            "weekly" => from.Date.AddDays(7),
            "monthly" => from.Date.AddMonths(1),
            _ => from.Date.AddDays(1),
        };
    }
}