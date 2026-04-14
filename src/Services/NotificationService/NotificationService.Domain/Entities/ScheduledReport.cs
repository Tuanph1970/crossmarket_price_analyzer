using Common.Domain;
using Common.Domain.Entities;

namespace NotificationService.Domain.Entities;

/// <summary>
/// Scheduled report subscription for periodic opportunity digest emails.
/// </summary>
public sealed class ScheduledReport : BaseEntity<Guid>
{
    public Guid UserId { get; private set; }
    public string ReportName { get; private set; } = string.Empty;
    public string Schedule { get; private set; } = "daily"; // daily | weekly | monthly
    public string Format { get; private set; } = "pdf"; // pdf | csv
    public bool IsActive { get; private set; } = true;
    public DateTime? NextRunAt { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    private ScheduledReport() { } // EF Core

    public static ScheduledReport Create(
        Guid userId,
        string reportName,
        string schedule = "daily",
        string format = "pdf")
    {
        return new ScheduledReport
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ReportName = reportName,
            Schedule = schedule,
            Format = format,
            IsActive = true,
            NextRunAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
        };
    }

    public void MarkRun(DateTime ranAt, DateTime? nextRun)
    {
        NextRunAt = nextRun;
        UpdatedAt = ranAt;
    }

    public void Deactivate()
    {
        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
    }
}
