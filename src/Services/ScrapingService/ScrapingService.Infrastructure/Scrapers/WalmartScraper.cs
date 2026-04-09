using System.Text.RegularExpressions;
using Common.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace ScrapingService.Infrastructure.Scrapers;

public class WalmartScraper : IProductScraper
{
    private readonly ILogger<WalmartScraper> _logger;
    public ProductSource Source => ProductSource.Walmart;

    public WalmartScraper(ILogger<WalmartScraper> logger) { _logger = logger; }

    public bool CanHandle(string url) => url.Contains("walmart.com", StringComparison.OrdinalIgnoreCase);

    public async Task<ScrapedProduct?> ScrapeAsync(string url, CancellationToken ct = default)
    {
        try
        {
            var pw = await Microsoft.Playwright.Playwright.CreateAsync();
            var browser = await pw.Chromium.LaunchAsync(new() { Headless = true });
            var page = await browser.NewPageAsync();
            await page.GotoAsync(url, new() { WaitUntil = WaitUntilState.NetworkIdle });
            await Task.Delay(2000, ct);

            var nameEl = await page.QuerySelectorAsync("h1[itemprop='name']")
                ?? await page.QuerySelectorAsync("[data-automation='product-title']");
            var name = nameEl != null ? await nameEl.TextContentAsync() ?? "" : "";

            var priceEl = await page.QuerySelectorAsync("[itemprop='price']")
                ?? await page.QuerySelectorAsync("[data-automation='product-price']");
            var priceText = priceEl != null ? await priceEl.TextContentAsync() ?? "" : "";
            var brandEl = await page.QuerySelectorAsync("[itemprop='brand']");
            var brand = brandEl != null ? await brandEl.TextContentAsync() ?? "" : "";

            await browser.CloseAsync();

            decimal price = 0;
            var clean = Regex.Replace(priceText, "[^0-9.]", "");
            decimal.TryParse(clean, out price);

            if (string.IsNullOrWhiteSpace(name) || price == 0)
            {
                _logger.LogWarning("Walmart incomplete {Url}", url);
                return null;
            }

            return new ScrapedProduct(
                name.Trim(), brand.Trim(), null, price, "USD", 1m,
                "Walmart", null, null, url, ProductSource.Walmart);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Walmart scrape failed {Url}", url);
            return null;
        }
    }

    public async Task<IReadOnlyList<string>> GetProductUrlsAsync(int count, CancellationToken ct = default)
    {
        var urls = new List<string>();
        foreach (var term in new[] { "electronics", "vitamins", "coffee" })
        {
            if (urls.Count >= count) break;
            try
            {
                var pw = await Microsoft.Playwright.Playwright.CreateAsync();
                var browser = await pw.Chromium.LaunchAsync(new() { Headless = true });
                var page = await browser.NewPageAsync();
                await page.GotoAsync(
                    $"https://www.walmart.com/search?q={term}",
                    new() { WaitUntil = WaitUntilState.NetworkIdle });
                await Task.Delay(1000, ct);
                var links = await page.QuerySelectorAllAsync("a[href*='/ip/']");
                foreach (var l in links.Take(count - urls.Count))
                {
                    var href = await l.GetAttributeAsync("href") ?? "";
                    if (href.Contains("/ip/"))
                    {
                        var full = href.StartsWith("http") ? href : "https://www.walmart.com" + href;
                        if (!urls.Contains(full)) urls.Add(full);
                    }
                }
                await browser.CloseAsync();
            }
            catch { /* skip */ }
        }
        return urls;
    }
}
