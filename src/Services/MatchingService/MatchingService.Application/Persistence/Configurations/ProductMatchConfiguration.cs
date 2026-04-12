using Common.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MatchingService.Domain.Entities;

namespace MatchingService.Application.Persistence.Configurations;

public class ProductMatchConfiguration : IEntityTypeConfiguration<ProductMatch>
{
    public void Configure(EntityTypeBuilder<ProductMatch> builder)
    {
        builder.ToTable("product_matches");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("id").HasColumnType("char(36)");

        builder.Property(p => p.UsProductId).HasColumnName("us_product_id").HasColumnType("char(36)").IsRequired();
        builder.Property(p => p.VnProductId).HasColumnName("vn_product_id").HasColumnType("char(36)").IsRequired();
        builder.Property(p => p.ConfidenceScore).HasColumnName("confidence_score").HasColumnType("decimal(5,2)").IsRequired();
        builder.Property(p => p.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(p => p.ConfirmedBy).HasColumnName("confirmed_by").HasMaxLength(100);
        builder.Property(p => p.ConfirmedAt).HasColumnName("confirmed_at").HasColumnType("datetime(6)");
        builder.Property(p => p.CreatedAt).HasColumnName("created_at").HasColumnType("datetime(6)");
        builder.Property(p => p.UpdatedAt).HasColumnName("updated_at").HasColumnType("datetime(6)");

        builder.HasIndex(p => p.Status).HasDatabaseName("idx_status");
        builder.HasIndex(p => p.UsProductId).HasDatabaseName("idx_us_product");
        builder.HasIndex(p => p.ConfidenceScore).HasDatabaseName("idx_confidence");
        // P2-B04: missing production indexes
        builder.HasIndex(p => p.VnProductId).HasDatabaseName("idx_vn_product");
        builder.HasIndex(p => new { p.UsProductId, p.VnProductId }).HasDatabaseName("idx_us_vn_product");
        builder.HasIndex(p => new { p.Status, p.ConfidenceScore }).HasDatabaseName("idx_status_confidence");
    }
}
