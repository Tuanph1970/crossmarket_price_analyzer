using Common.Domain.Entities;

namespace ProductService.Domain.Entities;

public class Category : AuditableEntity<Guid>
{
    public string Name { get; set; } = string.Empty;
    public string HsCode { get; set; } = string.Empty;
    public Guid? ParentCategoryId { get; set; }
    public Category? ParentCategory { get; set; }
    public ICollection<Category> SubCategories { get; set; } = new List<Category>();
    public ICollection<Product> Products { get; set; } = new List<Product>();
}
