using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProductService.Domain.Entities;

namespace ProductService.Infrastructure.Persistence.Configurations;

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("products");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("id").HasColumnType("char(36)");

        builder.Property(p => p.Name).HasColumnName("name").HasMaxLength(500).IsRequired();
        builder.Property(p => p.Sku).HasColumnName("sku").HasMaxLength(100);
        builder.Property(p => p.HsCode).HasColumnName("hs_code").HasMaxLength(20);
        builder.Property(p => p.Source).HasColumnName("source").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(p => p.SourceUrl).HasColumnName("source_url").HasMaxLength(2000).IsRequired();
        builder.Property(p => p.IsActive).HasColumnName("is_active").HasDefaultValue(true);
        builder.Property(p => p.CreatedAt).HasColumnName("created_at").HasColumnType("datetime(6)");
        builder.Property(p => p.UpdatedAt).HasColumnName("updated_at").HasColumnType("datetime(6)");

        builder.HasOne(p => p.Brand).WithMany(b => b.Products).HasForeignKey(p => p.BrandId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(p => p.Category).WithMany(c => c.Products).HasForeignKey(p => p.CategoryId).OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(p => p.Source).HasDatabaseName("idx_source");
        builder.HasIndex(p => p.BrandId).HasDatabaseName("idx_brand");
        builder.HasIndex(p => p.CategoryId).HasDatabaseName("idx_category");
        builder.HasIndex(p => p.HsCode).HasDatabaseName("idx_hs_code");
    }
}
