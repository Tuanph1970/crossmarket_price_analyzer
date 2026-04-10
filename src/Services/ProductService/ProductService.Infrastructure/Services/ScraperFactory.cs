using Common.Application.Interfaces;
using Common.Domain.Enums;
using Common.Domain.Scraping;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace ProductService.Infrastructure.Services;

/// <summary>
/// Scraper factory that resolves the correct IProductScraper by URL or source.
/// Lives in ProductService.Infrastructure so it can use Playwright scrapers directly.
/// </summary>
public class ScraperFactory : IScraperFactory
{
    private readonly IEnumerable<IProductScraper> _scrapers;
    private readonly ILogger<ScraperFactory> _logger;

    public ScraperFactory(IEnumerable<IProductScraper> scrapers, ILogger<ScraperFactory> logger)
    {
        _scrapers = scrapers.ToList();
        _logger = logger;
    }

    public IReadOnlyList<IProductScraper> GetAll() => _scrapers.ToList();

    public IProductScraper? GetForUrl(string url)
    {
        var scraper = _scrapers.FirstOrDefault(s => s.CanHandle(url));
        if (scraper is null)
            _logger.LogWarning("No scraper found for URL: {Url}", url);
        return scraper;
    }

    public IProductScraper? GetForSource(ProductSource source) =>
        _scrapers.FirstOrDefault(s => s.Source == source);
}
