using Common.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using MatchingService.Domain.Entities;

namespace MatchingService.Application.Persistence;

public class MatchingDbContext : BaseDbContext
{
    public MatchingDbContext(DbContextOptions<MatchingDbContext> options)
        : base(options) { }

    public DbSet<ProductMatch> ProductMatches => Set<ProductMatch>();
    public DbSet<MatchConfirmation> MatchConfirmations => Set<MatchConfirmation>();
}
