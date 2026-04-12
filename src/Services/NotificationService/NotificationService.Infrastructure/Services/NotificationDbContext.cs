using Microsoft.EntityFrameworkCore;
using NotificationService.Domain.Entities;

namespace NotificationService.Infrastructure.Services;

/// <summary>
/// P4-B04: NotificationService's own DbContext.
/// Stores alert preferences, delivery logs, and scheduled report configs per user.
/// </summary>
public sealed class NotificationDbContext : DbContext
{
    public DbSet<AlertPreference> AlertPreferences => Set<AlertPreference>();
    public DbSet<DeliveryLog> DeliveryLogs => Set<DeliveryLog>();
    public DbSet<ScheduledReport> ScheduledReports => Set<ScheduledReport>();
    public DbSet<UserTelegramConfig> UserTelegramConfigs => Set<UserTelegramConfig>();

    public NotificationDbContext(DbContextOptions<NotificationDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<AlertPreference>(e =>
        {
            e.ToTable("alert_preferences");
            e.HasKey(a => a.Id);
            e.HasIndex(a => a.UserId);
        });

        modelBuilder.Entity<DeliveryLog>(e =>
        {
            e.ToTable("delivery_logs");
            e.HasKey(d => d.Id);
            d.HasIndex(d => new { d.UserId, d.SentAt });
        });

        modelBuilder.Entity<ScheduledReport>(e =>
        {
            e.ToTable("scheduled_reports");
            e.HasKey(s => s.Id);
            e.HasIndex(s => s.UserId);
        });

        modelBuilder.Entity<UserTelegramConfig>(e =>
        {
            e.ToTable("user_telegram_configs");
            e.HasKey(t => t.Id);
            e.HasIndex(t => t.UserId).IsUnique();
        });
    }
}

// ── Domain entities (defined in Domain layer) ─────────────────────────────────

namespace NotificationService.Domain.Entities;

using Common.Domain.Entities;

public sealed class AlertPreference : AuditableEntity<Guid>
{
    public Guid UserId { get; private set; }
    public Common.Domain.Enums.DeliveryChannel Channel { get; private set; }
    public bool IsEnabled { get; private set; } = true;

    /// <summary>Minimum composite score to trigger an alert.</summary>
    public decimal MinScoreThreshold { get; private set; }

    /// <summary>Minimum profit margin % to trigger.</summary>
    public decimal? MinMarginThreshold { get; private set; }

    /// <summary>Email address for Email channel; Telegram chat_id for Telegram channel.</summary>
    public string DeliveryTarget { get; private set; } = string.Empty;

    /// <summary>Minimum score change delta to avoid spamming.</summary>
    public decimal MinScoreDelta { get; private set; } = 5m;

    public static AlertPreference Create(
        Guid userId,
        Common.Domain.Enums.DeliveryChannel channel,
        decimal minScore,
        string deliveryTarget,
        decimal? minMargin = null)
    {
        return new AlertPreference
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Channel = channel,
            MinScoreThreshold = minScore,
            DeliveryTarget = deliveryTarget,
            MinMarginThreshold = minMargin,
            IsEnabled = true,
        };
    }

    public void SetEnabled(bool enabled) => IsEnabled = enabled;
    public void UpdateThresholds(decimal minScore, decimal? minMargin, decimal minDelta)
    {
        MinScoreThreshold = minScore;
        MinMarginThreshold = minMargin;
        MinScoreDelta = minDelta;
    }
}

public sealed class DeliveryLog : AuditableEntity<Guid>
{
    public Guid UserId { get; private set; }
    public Guid? MatchId { get; private set; }
    public Common.Domain.Enums.DeliveryChannel Channel { get; private set; }
    public string DeliveryTarget { get; private set; } = string.Empty;
    public string Subject { get; private set; } = string.Empty;
    public bool WasSuccessful { get; private set; }
    public string? FailureReason { get; private set; }
    public DateTime SentAt { get; private set; } = DateTime.UtcNow;

    public static DeliveryLog Create(
        Guid userId,
        Common.Domain.Enums.DeliveryChannel channel,
        string deliveryTarget,
        string subject,
        bool success,
        string? failureReason = null,
        Guid? matchId = null)
    {
        return new DeliveryLog
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            MatchId = matchId,
            Channel = channel,
            DeliveryTarget = deliveryTarget,
            Subject = subject,
            WasSuccessful = success,
            FailureReason = failureReason,
            SentAt = DateTime.UtcNow,
        };
    }
}

public sealed class ScheduledReport : AuditableEntity<Guid>
{
    public Guid UserId { get; private set; }
    public string ReportName { get; private set; } = string.Empty;
    public string Schedule { get; private set; } = "daily"; // "daily", "weekly", "monthly"
    public string Format { get; private set; } = "pdf";    // "pdf" or "csv"
    public string DeliveryEmail { get; private set; } = string.Empty;
    public bool IsActive { get; private set; } = true;
    public DateTime? LastRunAt { get; private set; }
    public DateTime? NextRunAt { get; private set; }

    public static ScheduledReport Create(
        Guid userId,
        string name,
        string schedule,
        string format,
        string deliveryEmail)
    {
        return new ScheduledReport
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ReportName = name,
            Schedule = schedule,
            Format = format,
            DeliveryEmail = deliveryEmail,
            IsActive = true,
        };
    }

    public void MarkRun(DateTime now, DateTime nextRun) { LastRunAt = now; NextRunAt = nextRun; }
    public void Deactivate() => IsActive = false;
}

public sealed class UserTelegramConfig : AuditableEntity<Guid>
{
    public Guid UserId { get; private set; }
    public string TelegramChatId { get; private set; } = string.Empty;
    public string? TelegramUsername { get; private set; }

    public static UserTelegramConfig Create(Guid userId, string chatId, string? username = null)
    {
        return new UserTelegramConfig
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TelegramChatId = chatId,
            TelegramUsername = username,
        };
    }
}