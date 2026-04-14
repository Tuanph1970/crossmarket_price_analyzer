using Common.Domain.Entities;
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
            e.HasIndex(d => new { d.UserId, d.SentAt });
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
