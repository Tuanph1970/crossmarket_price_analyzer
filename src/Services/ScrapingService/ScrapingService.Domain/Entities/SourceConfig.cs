using Common.Domain.Entities;
using Common.Domain.Enums;

namespace ScrapingService.Domain.Entities;

public class SourceConfig : BaseEntity<Guid>
{
    public string SourceName { get; set; } = string.Empty;
    public ProductSource Source { get; set; }
    public string BaseUrl { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public string? RobotsTxtUrl { get; set; }
    public int RateLimitPerHour { get; set; } = 100;
    public string? ApiKey { get; set; }
    public int Priority { get; set; }
}
