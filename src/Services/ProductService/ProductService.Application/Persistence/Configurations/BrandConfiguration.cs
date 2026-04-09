using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProductService.Domain.Entities;

namespace ProductService.Application.Persistence.Configurations;

public class BrandConfiguration : IEntityTypeConfiguration<Brand>
{
    public void Configure(EntityTypeBuilder<Brand> builder)
    {
        builder.ToTable("brands");

        builder.HasKey(b => b.Id);
        builder.Property(b => b.Id).HasColumnName("id").HasColumnType("char(36)");

        builder.Property(b => b.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(b => b.NormalizedName).HasColumnName("normalized_name").HasMaxLength(200);
        builder.Property(b => b.CreatedAt).HasColumnName("created_at").HasColumnType("datetime(6)");

        builder.HasIndex(b => b.NormalizedName).IsUnique().HasDatabaseName("uk_brand_normalized");
    }
}
