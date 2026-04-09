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

    /// <summary>
    /// Factory method — creates a new Category with auto-generated Id.
    /// </summary>
    public static Category Create(string name, string hsCode, Guid? parentId = null)
    {
        return new Category
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            HsCode = hsCode.Trim(),
            ParentCategoryId = parentId,
            CreatedAt = DateTime.UtcNow
        };
    }
}
