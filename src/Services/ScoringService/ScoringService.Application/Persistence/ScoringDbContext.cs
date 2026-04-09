using Microsoft.EntityFrameworkCore;
using Common.Infrastructure.Persistence;
using ScoringService.Domain.Entities;

namespace ScoringService.Application.Persistence;

public class ScoringDbContext : BaseDbContext
{
    public ScoringDbContext(DbContextOptions<ScoringDbContext> options) : base(options) { }
    public DbSet<OpportunityScore> OpportunityScores => Set<OpportunityScore>();
    public DbSet<ScoringConfig> ScoringConfigs => Set<ScoringConfig>();
}
