using Common.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using ProductService.Contracts.Persistence;
using ProductService.Domain.Entities;

namespace ProductService.Infrastructure.Persistence;

/// <summary>
/// ProductService-specific DbContext extending BaseDbContext and implementing IProductDbContext.
/// </summary>
public class ProductDbContext : BaseDbContext, IProductDbContext
{
    public ProductDbContext(DbContextOptions<ProductDbContext> options)
        : base(options) { }

    public DbSet<Product> Products => Set<Product>();
    public DbSet<Brand> Brands => Set<Brand>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<PriceSnapshot> PriceSnapshots => Set<PriceSnapshot>();
    public DbSet<ExchangeRate> ExchangeRates => Set<ExchangeRate>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Product
        var product = modelBuilder.Entity<Product>();
        product.ToTable("products");
        product.HasKey(p => p.Id);
        product.Property(p => p.Id).HasColumnName("id").HasColumnType("char(36)");
        product.Property(p => p.Name).HasColumnName("name").HasMaxLength(500).IsRequired();
        product.Property(p => p.Sku).HasColumnName("sku").HasMaxLength(100);
        product.Property(p => p.HsCode).HasColumnName("hs_code").HasMaxLength(20);
        product.Property(p => p.Source).HasColumnName("source").HasConversion<string>().HasMaxLength(20).IsRequired();
        product.Property(p => p.SourceUrl).HasColumnName("source_url").HasMaxLength(2000).IsRequired();
        product.Property(p => p.IsActive).HasColumnName("is_active").HasDefaultValue(true);
        product.Property(p => p.CreatedAt).HasColumnName("created_at").HasColumnType("datetime(6)");
        product.Property(p => p.UpdatedAt).HasColumnName("updated_at").HasColumnType("datetime(6)");
        product.HasOne(p => p.Brand).WithMany(b => b.Products).HasForeignKey(p => p.BrandId).OnDelete(DeleteBehavior.SetNull);
        product.HasOne(p => p.Category).WithMany(c => c.Products).HasForeignKey(p => p.CategoryId).OnDelete(DeleteBehavior.SetNull);
        product.HasIndex(p => p.Source).HasDatabaseName("idx_source");
        product.HasIndex(p => p.BrandId).HasDatabaseName("idx_brand");
        product.HasIndex(p => p.CategoryId).HasDatabaseName("idx_category");
        product.HasIndex(p => p.HsCode).HasDatabaseName("idx_hs_code");

        // Brand
        var brand = modelBuilder.Entity<Brand>();
        brand.ToTable("brands");
        brand.HasKey(b => b.Id);
        brand.Property(b => b.Id).HasColumnName("id").HasColumnType("char(36)");
        brand.Property(b => b.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        brand.Property(b => b.NormalizedName).HasColumnName("normalized_name").HasMaxLength(200);
        brand.Property(b => b.CreatedAt).HasColumnName("created_at").HasColumnType("datetime(6)");
        brand.HasIndex(b => b.NormalizedName).IsUnique().HasDatabaseName("uk_brand_normalized");

        // Category
        var category = modelBuilder.Entity<Category>();
        category.ToTable("categories");
        category.HasKey(c => c.Id);
        category.Property(c => c.Id).HasColumnName("id").HasColumnType("char(36)");
        category.Property(c => c.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        category.Property(c => c.HsCode).HasColumnName("hs_code").HasMaxLength(20).IsRequired();
        category.Property(c => c.ParentCategoryId).HasColumnName("parent_category_id").HasColumnType("char(36)");
        category.HasOne(c => c.ParentCategory).WithMany(c => c.SubCategories).HasForeignKey(c => c.ParentCategoryId).OnDelete(DeleteBehavior.Restrict);
        category.Property(c => c.CreatedAt).HasColumnName("created_at").HasColumnType("datetime(6)");
        category.HasIndex(c => c.HsCode).IsUnique().HasDatabaseName("uk_category_hs_code");

        // PriceSnapshot
        var snapshot = modelBuilder.Entity<PriceSnapshot>();
        snapshot.ToTable("price_snapshots");
        snapshot.HasKey(ps => ps.Id);
        snapshot.Property(ps => ps.Id).HasColumnName("id").HasColumnType("char(36)");
        snapshot.Property(ps => ps.ProductId).HasColumnName("product_id").HasColumnType("char(36)").IsRequired();
        snapshot.Property(ps => ps.Price).HasColumnName("price").HasColumnType("decimal(18,4)").IsRequired();
        snapshot.Property(ps => ps.Currency).HasColumnName("currency").HasConversion<string>().HasMaxLength(3).IsRequired();
        snapshot.Property(ps => ps.UnitPrice).HasColumnName("unit_price").HasColumnType("decimal(18,4)").IsRequired();
        snapshot.Property(ps => ps.QuantityPerUnit).HasColumnName("quantity_per_unit").HasColumnType("decimal(10,2)").HasDefaultValue(1);
        snapshot.Property(ps => ps.SellerName).HasColumnName("seller_name").HasMaxLength(200);
        snapshot.Property(ps => ps.SellerRating).HasColumnName("seller_rating").HasColumnType("decimal(3,2)");
        snapshot.Property(ps => ps.SalesVolume).HasColumnName("sales_volume");
        snapshot.Property(ps => ps.ScrapedAt).HasColumnName("scraped_at").HasColumnType("datetime(6)").IsRequired();
        snapshot.HasOne(ps => ps.Product).WithMany(p => p.PriceSnapshots).HasForeignKey(ps => ps.ProductId).OnDelete(DeleteBehavior.Cascade);
        snapshot.HasIndex(ps => new { ps.ProductId, ps.ScrapedAt }).IsDescending(false, true).HasDatabaseName("idx_product_time");

        // ExchangeRate
        var rate = modelBuilder.Entity<ExchangeRate>();
        rate.ToTable("exchange_rates");
        rate.HasKey(er => er.Id);
        rate.Property(er => er.Id).HasColumnName("id").HasColumnType("char(36)");
        rate.Property(er => er.FromCurrency).HasColumnName("from_currency").HasMaxLength(3).IsRequired();
        rate.Property(er => er.ToCurrency).HasColumnName("to_currency").HasMaxLength(3).IsRequired();
        rate.Property(er => er.Rate).HasColumnName("rate").HasColumnType("decimal(18,8)").IsRequired();
        rate.Property(er => er.FetchedAt).HasColumnName("fetched_at").HasColumnType("datetime(6)").IsRequired();
        rate.HasIndex(er => new { er.FromCurrency, er.ToCurrency }).IsUnique().HasDatabaseName("uk_pair");
        rate.HasIndex(er => er.FetchedAt).IsDescending(true).HasDatabaseName("idx_fetched");
    }
}
