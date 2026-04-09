using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MatchingService.Domain.Entities;

namespace MatchingService.Infrastructure.Persistence.Configurations;

public class MatchConfirmationConfiguration : IEntityTypeConfiguration<MatchConfirmation>
{
    public void Configure(EntityTypeBuilder<MatchConfirmation> builder)
    {
        builder.ToTable("match_confirmations");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("id").HasColumnType("char(36)");

        builder.Property(p => p.MatchId).HasColumnName("match_id").HasColumnType("char(36)").IsRequired();
        builder.Property(p => p.UserId).HasColumnName("user_id").HasMaxLength(100).IsRequired();
        builder.Property(p => p.Action).HasColumnName("action").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(p => p.Notes).HasColumnName("notes").HasColumnType("text");

        builder.HasOne<Domain.Entities.ProductMatch>(p => null!)
            .WithMany(m => m.Confirmations)
            .HasForeignKey(p => p.MatchId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
