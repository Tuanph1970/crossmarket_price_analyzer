using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProductService.Domain.Entities;

namespace ProductService.Infrastructure.Persistence.Configurations;

public class PriceSnapshotConfiguration : IEntityTypeConfiguration<PriceSnapshot>
{
    public void Configure(EntityTypeBuilder<PriceSnapshot> builder)
    {
        builder.ToTable("price_snapshots");

        builder.HasKey(ps => ps.Id);
        builder.Property(ps => ps.Id).HasColumnName("id").HasColumnType("char(36)");

        builder.Property(ps => ps.ProductId).HasColumnName("product_id").HasColumnType("char(36)").IsRequired();
        builder.Property(ps => ps.Price).HasColumnName("price").HasColumnType("decimal(18,4)").IsRequired();
        builder.Property(ps => ps.Currency).HasColumnName("currency").HasConversion<string>().HasMaxLength(3).IsRequired();
        builder.Property(ps => ps.UnitPrice).HasColumnName("unit_price").HasColumnType("decimal(18,4)").IsRequired();
        builder.Property(ps => ps.QuantityPerUnit).HasColumnName("quantity_per_unit").HasColumnType("decimal(10,2)").HasDefaultValue(1);
        builder.Property(ps => ps.SellerName).HasColumnName("seller_name").HasMaxLength(200);
        builder.Property(ps => ps.SellerRating).HasColumnName("seller_rating").HasColumnType("decimal(3,2)");
        builder.Property(ps => ps.SalesVolume).HasColumnName("sales_volume");
        builder.Property(ps => ps.ScrapedAt).HasColumnName("scraped_at").HasColumnType("datetime(6)").IsRequired();

        builder.HasOne(ps => ps.Product).WithMany(p => p.PriceSnapshots).HasForeignKey(ps => ps.ProductId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(ps => new { ps.ProductId, ps.ScrapedAt }).IsDescending(false, true).HasDatabaseName("idx_product_time");
        // P2-B04: index on ProductId alone for direct lookups
        builder.HasIndex(ps => ps.ProductId).HasDatabaseName("idx_product_id");
    }
}
