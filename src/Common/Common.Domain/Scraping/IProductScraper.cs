using Common.Domain.Enums;

namespace Common.Domain.Scraping;

/// <summary>
/// Result of a successful product scrape from any source.
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
/// Placed in Common.Domain so any service can use scrapers without cross-service HTTP calls.
/// </summary>
public interface IProductScraper
{
    ProductSource Source { get; }
    bool CanHandle(string url);
    Task<ScrapedProduct?> ScrapeAsync(string url, CancellationToken ct = default);
    Task<IReadOnlyList<string>> GetProductUrlsAsync(int count, CancellationToken ct = default);

    /// <summary>
    /// Fetches a category/listing page and returns individual product URLs found on it.
    /// Default implementation returns empty — override in scrapers that support listing pages.
    /// </summary>
    Task<IReadOnlyList<string>> GetProductUrlsFromPageAsync(string pageUrl, int maxCount, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());

    /// <summary>
    /// Scrapes a category/listing page and returns all products found in a single fetch.
    /// More efficient than calling GetProductUrlsFromPageAsync + ScrapeAsync per product.
    /// Default implementation returns empty — override in scrapers that support listing pages.
    /// </summary>
    Task<IReadOnlyList<ScrapedProduct>> ScrapeListingDirectAsync(string pageUrl, int maxCount, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<ScrapedProduct>>(Array.Empty<ScrapedProduct>());
}
