using Common.Domain.Entities;

namespace ScrapingService.Domain.Entities;

public class ScrapeJob : AuditableEntity<Guid>
{
    public string Source { get; set; } = string.Empty;
    public string JobType { get; set; } = string.Empty;
    public ScrapeJobStatus Status { get; set; } = ScrapeJobStatus.Scheduled;
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int ItemsScraped { get; set; }
    public string? ErrorMessage { get; set; }
}

public enum ScrapeJobStatus
{
    Scheduled,
    Running,
    Completed,
    Failed
}
