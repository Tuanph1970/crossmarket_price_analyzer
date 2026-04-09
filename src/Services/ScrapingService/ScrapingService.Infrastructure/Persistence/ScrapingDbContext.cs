using Common.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using ScrapingService.Domain.Entities;

namespace ScrapingService.Infrastructure.Persistence;

public class ScrapingDbContext : BaseDbContext
{
    public ScrapingDbContext(DbContextOptions<ScrapingDbContext> options)
        : base(options) { }

    public DbSet<ScrapeJob> ScrapeJobs => Set<ScrapeJob>();
    public DbSet<SourceConfig> SourceConfigs => Set<SourceConfig>();
}
