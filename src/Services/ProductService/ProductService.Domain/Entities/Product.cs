using Common.Domain.Entities;
using Common.Domain.Enums;

namespace ProductService.Domain.Entities;

public class Product : AuditableEntity<Guid>
{
    public string Name { get; set; } = string.Empty;
    public Guid? BrandId { get; set; }
    public Brand? Brand { get; set; }
    public string? Sku { get; set; }
    public Guid? CategoryId { get; set; }
    public Category? Category { get; set; }
    public string? HsCode { get; set; }
    public ProductSource Source { get; set; }
    public string SourceUrl { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public ICollection<PriceSnapshot> PriceSnapshots { get; set; } = new List<PriceSnapshot>();

    /// <summary>
    /// Factory method — creates a new Product with auto-generated Id.
    /// Use this from any layer where direct entity construction with Id is needed.
    /// </summary>
    public static Product Create(
        string name,
        string sourceUrl,
        ProductSource source,
        string? sku = null,
        string? hsCode = null,
        Guid? brandId = null,
        Guid? categoryId = null,
        bool isActive = true)
    {
        return new Product
        {
            Id = Guid.NewGuid(),
            Name = name,
            SourceUrl = sourceUrl,
            Source = source,
            Sku = sku,
            HsCode = hsCode,
            BrandId = brandId,
            CategoryId = categoryId,
            IsActive = isActive,
            CreatedAt = DateTime.UtcNow
        };
    }
}