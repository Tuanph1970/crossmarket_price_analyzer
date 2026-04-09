using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ScoringService.Domain.Entities;

namespace ScoringService.Application.Persistence.Configurations;

public class ScoringConfigConfiguration : IEntityTypeConfiguration<ScoringConfig>
{
    public void Configure(EntityTypeBuilder<ScoringConfig> builder)
    {
        builder.ToTable("scoring_configs");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("id").HasColumnType("char(36)");
        builder.Property(p => p.FactorKey).HasColumnName("factor_key").HasMaxLength(50).IsRequired();
        builder.Property(p => p.Weight).HasColumnName("weight").HasColumnType("decimal(5,2)");
        builder.Property(p => p.MinThreshold).HasColumnName("min_threshold").HasColumnType("decimal(5,2)");
        builder.Property(p => p.MaxThreshold).HasColumnName("max_threshold").HasColumnType("decimal(5,2)");
        builder.Property(p => p.IsActive).HasColumnName("is_active").HasDefaultValue(true);

        builder.HasIndex(p => p.FactorKey).IsUnique().HasDatabaseName("uk_factor_key");
    }
}
