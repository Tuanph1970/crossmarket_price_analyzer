using Common.Domain.Entities;

namespace ProductService.Domain.Entities;

public class PriceSnapshot : BaseEntity<Guid>
{
    public Guid ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public decimal Price { get; set; }
    public string Currency { get; set; } = "USD";
    public decimal UnitPrice { get; set; }
    public decimal QuantityPerUnit { get; set; } = 1;
    public string? SellerName { get; set; }
    public decimal? SellerRating { get; set; }
    public int? SalesVolume { get; set; }
    public DateTime ScrapedAt { get; set; }
}
