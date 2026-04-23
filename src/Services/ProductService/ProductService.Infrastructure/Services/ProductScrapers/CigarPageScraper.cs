using Common.Domain.Enums;
using Common.Domain.Scraping;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace ProductService.Infrastructure.Services.ProductScrapers;

/// <summary>
/// cigarpage.com scraper using Playwright.
/// </summary>
public class CigarPageScraper : IProductScraper
{
    private readonly ILogger<CigarPageScraper> _logger;
    public ProductSource Source => ProductSource.CigarPage;

    public CigarPageScraper(ILogger<CigarPageScraper> logger) { _logger = logger; }

    public bool CanHandle(string url) =>
        url.Contains("cigarpage.com", StringComparison.OrdinalIgnoreCase);

    public async Task<ScrapedProduct?> ScrapeAsync(string url, CancellationToken ct = default)
    {
        try
        {
            using var pw = await Playwright.CreateAsync();
            var browser = await pw.Chromium.LaunchAsync(new()
            {
                Headless = true,
                Args = new[] { "--no-sandbox", "--disable-setuid-sandbox", "--disable-dev-shm-usage" },
            });
            var context = await browser.NewContextAsync(new()
            {
                UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36",
                ViewportSize = new ViewportSize { Width = 1920, Height = 1080 },
            });
            var page = await context.NewPageAsync();
            await page.GotoAsync(url, new() { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 30000 });
            await Task.Delay(2000, ct);

            var nameEl  = await page.QuerySelectorAsync("h1.product-title");
            var priceEl = await page.QuerySelectorAsync("span.price");
            var brandEl = await page.QuerySelectorAsync("span.product-brand");

            var name      = nameEl  != null ? (await nameEl.TextContentAsync() ?? "").Trim() : "";
            var priceText = priceEl != null ? (await priceEl.TextContentAsync() ?? "").Trim() : "";
            var brand     = brandEl != null ? (await brandEl.TextContentAsync() ?? "").Trim() : "";
            await browser.CloseAsync();

            decimal price = 0;
            var clean = priceText.Replace("$", "").Replace(",", "").Trim();
            decimal.TryParse(clean, out price);

            if (string.IsNullOrWhiteSpace(name) || price == 0)
            {
                _logger.LogWarning("CigarPage scrape incomplete for {Url}", url);
                return null;
            }

            return new ScrapedProduct(
                name.Trim(), brand.Trim(), null, price, "USD", 1m,
                null, null, null, url, ProductSource.CigarPage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CigarPage scrape failed for {Url}", url);
            return null;
        }
    }

    public Task<IReadOnlyList<string>> GetProductUrlsAsync(int count, CancellationToken ct = default)
    {
        return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
    }
}
