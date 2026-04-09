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

    /// <summary>
    /// Factory method — creates a new PriceSnapshot with auto-generated Id.
    /// Use this from any layer where direct entity construction with Id is needed.
    /// </summary>
    public static PriceSnapshot Create(
        Guid productId,
        decimal price,
        string currency,
        decimal quantityPerUnit,
        string? sellerName = null,
        decimal? sellerRating = null,
        int? salesVolume = null)
    {
        return new PriceSnapshot
        {
            Id = Guid.NewGuid(),
            ProductId = productId,
            Price = price,
            Currency = currency,
            UnitPrice = price,
            QuantityPerUnit = quantityPerUnit,
            SellerName = sellerName,
            SellerRating = sellerRating,
            SalesVolume = salesVolume,
            ScrapedAt = DateTime.UtcNow
        };
    }
}