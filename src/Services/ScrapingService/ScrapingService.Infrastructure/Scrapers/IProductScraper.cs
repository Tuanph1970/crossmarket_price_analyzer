using Common.Domain.Enums;

namespace ScrapingService.Infrastructure.Scrapers;

/// <summary>
/// Result of a successful product scrape.
/// </summary>
public record ScrapedProduct(
    string Name,
    string? Brand,
    string? Sku,
    decimal Price,
    string Currency,
    decimal QuantityPerUnit,
    string? SellerName,
    decimal? SellerRating,
    int? SalesVolume,
    string SourceUrl,
    ProductSource Source
);

/// <summary>
/// Interface for source-specific product scrapers.
/// </summary>
public interface IProductScraper
{
    ProductSource Source { get; }
    bool CanHandle(string url);
    Task<ScrapedProduct?> ScrapeAsync(string url, CancellationToken ct = default);
    Task<IReadOnlyList<string>> GetProductUrlsAsync(int count, CancellationToken ct = default);
}
