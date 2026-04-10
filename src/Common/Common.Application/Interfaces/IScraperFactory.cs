using Common.Domain.Enums;
using Common.Domain.Scraping;

namespace Common.Application.Interfaces;

/// <summary>
/// Factory for obtaining registered IProductScraper instances.
/// Implemented by ScrapingService.Infrastructure and registered at startup.
/// Used by ProductService to resolve scrapers at runtime without a direct project reference.
/// </summary>
public interface IScraperFactory
{
    /// <summary>
    /// Returns all registered scrapers.
    /// </summary>
    IReadOnlyList<IProductScraper> GetAll();

    /// <summary>
    /// Returns the scraper that can handle the given URL, or null.
    /// </summary>
    IProductScraper? GetForUrl(string url);

    /// <summary>
    /// Returns the scraper for a specific source, or null.
    /// </summary>
    IProductScraper? GetForSource(ProductSource source);
}
