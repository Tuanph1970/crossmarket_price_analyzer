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
}
