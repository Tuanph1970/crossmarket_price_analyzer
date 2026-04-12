using AuthService.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AuthService.Application.Persistence;

/// <summary>
/// Entity Framework Core DbContext for the AuthService database.
/// Stores users, watchlists, and alert thresholds.
/// </summary>
public sealed class AuthDbContext : DbContext
{
    public DbSet<User> Users => Set<User>();
    public DbSet<WatchlistItem> WatchlistItems => Set<WatchlistItem>();
    public DbSet<AlertThreshold> AlertThresholds => Set<AlertThreshold>();

    public AuthDbContext(DbContextOptions<AuthDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(e =>
        {
            e.ToTable("users");
            e.HasKey(u => u.Id);
            e.Property(u => u.Email).HasMaxLength(256).IsRequired();
            e.HasIndex(u => u.Email).IsUnique();
            e.Property(u => u.PasswordHash).HasMaxLength(256).IsRequired();
            e.Property(u => u.FullName).HasMaxLength(200);
            e.Property(u => u.RefreshToken).HasMaxLength(256);
        });

        modelBuilder.Entity<WatchlistItem>(e =>
        {
            e.ToTable("watchlist_items");
            e.HasKey(w => w.Id);
            e.HasIndex(w => new { w.UserId, w.ProductMatchId }).IsUnique();
            e.HasOne<User>()
                .WithMany()
                .HasForeignKey(w => w.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AlertThreshold>(e =>
        {
            e.ToTable("alert_thresholds");
            e.HasKey(a => a.Id);
            e.HasIndex(a => a.UserId);
            e.HasOne<User>()
                .WithMany()
                .HasForeignKey(a => a.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
