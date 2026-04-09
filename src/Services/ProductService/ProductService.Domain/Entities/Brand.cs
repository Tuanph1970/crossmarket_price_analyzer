using Common.Domain.Entities;

namespace ProductService.Domain.Entities;

public class Brand : AuditableEntity<Guid>
{
    public string Name { get; set; } = string.Empty;
    public string NormalizedName { get; set; } = string.Empty;
    public ICollection<Product> Products { get; set; } = new List<Product>();

    /// <summary>
    /// Factory method — creates a new Brand with auto-generated Id.
    /// </summary>
    public static Brand Create(string name)
    {
        return new Brand
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            NormalizedName = name.ToLowerInvariant().Trim(),
            CreatedAt = DateTime.UtcNow
        };
    }
}
