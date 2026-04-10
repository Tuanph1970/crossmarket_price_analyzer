using System.Text.RegularExpressions;
using Common.Domain.Enums;
using Common.Domain.Scraping;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace ProductService.Infrastructure.Services.ProductScrapers;

/// <summary>
/// Amazon product scraper using Playwright.
/// </summary>
public class AmazonScraper : IProductScraper
{
    private readonly ILogger<AmazonScraper> _logger;
    public ProductSource Source => ProductSource.Amazon;

    public AmazonScraper(ILogger<AmazonScraper> logger) { _logger = logger; }

    public bool CanHandle(string url) =>
        url.Contains("amazon.com", StringComparison.OrdinalIgnoreCase);

    public async Task<ScrapedProduct?> ScrapeAsync(string url, CancellationToken ct = default)
    {
        try
        {
            using var pw = await Playwright.CreateAsync();
            var browser = await pw.Chromium.LaunchAsync(new() { Headless = true });
            var page = await browser.NewPageAsync();
            await page.GotoAsync(url, new() { WaitUntil = WaitUntilState.NetworkIdle });
            await Task.Delay(2000, ct);

            var nameEl  = await page.QuerySelectorAsync("#productTitle");
            var priceEl = await page.QuerySelectorAsync(".a-offscreen");
            var brandEl = await page.QuerySelectorAsync("#bylineInfo");
            var asinEl  = await page.QuerySelectorAsync("[data-asin]");

            var name  = nameEl  != null ? await nameEl.TextContentAsync() ?? "" : "";
            var priceText = priceEl != null ? await priceEl.TextContentAsync() ?? "" : "";
            var brand = brandEl != null ? await brandEl.TextContentAsync() ?? "" : "";
            var asin  = asinEl  != null ? await asinEl.GetAttributeAsync("data-asin") ?? "" : "";
            await browser.CloseAsync();

            decimal price = 0;
            var clean = Regex.Replace(priceText, "[^0-9.]", "");
            decimal.TryParse(clean, out price);
            var cleanBrand = brand
                .Replace("Brand: ", "")
                .Replace("Visit the ", "")
                .Replace(" Store", "")
                .Trim();

            if (string.IsNullOrWhiteSpace(name) || price == 0)
            {
                _logger.LogWarning("Amazon scrape incomplete for {Url}", url);
                return null;
            }

            return new ScrapedProduct(
                name.Trim(), cleanBrand, asin, price, "USD", 1m,
                null, null, null, url, ProductSource.Amazon);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Amazon scrape failed for {Url}", url);
            return null;
        }
    }

    public async Task<IReadOnlyList<string>> GetProductUrlsAsync(int count, CancellationToken ct = default)
    {
        var urls = new List<string>();
        foreach (var s in new[] { "electronics", "vitamins", "coffee" })
        {
            if (urls.Count >= count) break;
            try
            {
                using var pw = await Playwright.CreateAsync();
                var browser = await pw.Chromium.LaunchAsync(new() { Headless = true });
                var page = await browser.NewPageAsync();
                await page.GotoAsync($"https://www.amazon.com/s?k={s}",
                    new() { WaitUntil = WaitUntilState.NetworkIdle });
                await Task.Delay(1000, ct);
                var links = await page.QuerySelectorAllAsync("a.a-link-normal.s-no-outline");
                foreach (var l in links.Take(count - urls.Count))
                {
                    var href = await l.GetAttributeAsync("href") ?? "";
                    var full = href.StartsWith("http") ? href : "https://www.amazon.com" + href;
                    if (!urls.Contains(full)) urls.Add(full);
                }
                await browser.CloseAsync();
            }
            catch { /* skip individual failures */ }
        }
        return urls;
    }
}
