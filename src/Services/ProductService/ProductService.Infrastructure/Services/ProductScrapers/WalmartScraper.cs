using Common.Domain.Enums;
using Common.Domain.Scraping;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace ProductService.Infrastructure.Services.ProductScrapers;

/// <summary>
/// Walmart product scraper using Playwright.
/// </summary>
public class WalmartScraper : IProductScraper
{
    private readonly ILogger<WalmartScraper> _logger;
    public ProductSource Source => ProductSource.Walmart;

    public WalmartScraper(ILogger<WalmartScraper> logger) { _logger = logger; }

    public bool CanHandle(string url) =>
        url.Contains("walmart.com", StringComparison.OrdinalIgnoreCase);

    public async Task<ScrapedProduct?> ScrapeAsync(string url, CancellationToken ct = default)
    {
        try
        {
            using var pw = await Playwright.CreateAsync();
            var browser = await pw.Chromium.LaunchAsync(new() { Headless = true });
            var page = await browser.NewPageAsync();
            await page.GotoAsync(url, new() { WaitUntil = WaitUntilState.NetworkIdle });
            await Task.Delay(2000, ct);

            var nameEl  = await page.QuerySelectorAsync("h1[itemprop='name']");
            var priceEl = await page.QuerySelectorAsync("[itemprop='price']");
            var brandEl = await page.QuerySelectorAsync("[itemprop='brand']");

            var name      = nameEl  != null ? await nameEl.TextContentAsync() ?? "" : "";
            var priceText = priceEl != null ? await priceEl.GetAttributeAsync("content") ?? "" : "";
            var brand     = brandEl != null ? await brandEl.TextContentAsync() ?? "" : "";
            await browser.CloseAsync();

            decimal price = 0;
            decimal.TryParse(priceText, out price);

            if (string.IsNullOrWhiteSpace(name) || price == 0)
            {
                _logger.LogWarning("Walmart scrape incomplete for {Url}", url);
                return null;
            }

            return new ScrapedProduct(
                name.Trim(), brand.Trim(), null, price, "USD", 1m,
                null, null, null, url, ProductSource.Walmart);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Walmart scrape failed for {Url}", url);
            return null;
        }
    }

    public Task<IReadOnlyList<string>> GetProductUrlsAsync(int count, CancellationToken ct = default)
    {
        // TODO: Implement category search URL generation for Walmart
        return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
    }
}
