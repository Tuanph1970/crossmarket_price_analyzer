using Common.Domain.Entities;

namespace ProductService.Domain.Entities;

public class Brand : AuditableEntity<Guid>
{
    public string Name { get; set; } = string.Empty;
    public string NormalizedName { get; set; } = string.Empty;
    public ICollection<Product> Products { get; set; } = new List<Product>();
}
