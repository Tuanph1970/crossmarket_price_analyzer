using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProductService.Domain.Entities;

namespace ProductService.Infrastructure.Persistence.Configurations;

public class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("categories");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("id").HasColumnType("char(36)");

        builder.Property(c => c.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(c => c.HsCode).HasColumnName("hs_code").HasMaxLength(20).IsRequired();
        builder.Property(c => c.ParentCategoryId).HasColumnName("parent_category_id").HasColumnType("char(36)");

        builder.HasOne(c => c.ParentCategory).WithMany(c => c.SubCategories).HasForeignKey(c => c.ParentCategoryId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(c => c.CreatedAt).HasColumnName("created_at").HasColumnType("datetime(6)");

        builder.HasIndex(c => c.HsCode).IsUnique().HasDatabaseName("uk_category_hs_code");
    }
}
