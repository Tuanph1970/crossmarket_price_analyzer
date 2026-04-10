using System.Text.RegularExpressions;
using Common.Domain.Enums;
using Common.Domain.Scraping;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace ScrapingService.Infrastructure.Scrapers;

public class CigarPageScraper : IProductScraper
{
    private readonly ILogger<CigarPageScraper> _logger;
    public ProductSource Source => ProductSource.CigarPage;

    public CigarPageScraper(ILogger<CigarPageScraper> logger) { _logger = logger; }

    public bool CanHandle(string url) =>
        url.Contains("cigarpage.com", StringComparison.OrdinalIgnoreCase) ||
        url.Contains("cigarpage", StringComparison.OrdinalIgnoreCase);

    public async Task<ScrapedProduct?> ScrapeAsync(string url, CancellationToken ct = default)
    {
        try
        {
            var pw = await Microsoft.Playwright.Playwright.CreateAsync();
            var browser = await pw.Chromium.LaunchAsync(new() { Headless = true });
            var page = await browser.NewPageAsync();
            await page.GotoAsync(url, new() { WaitUntil = WaitUntilState.NetworkIdle });
            await Task.Delay(2000, ct);

            var nameEl = await page.QuerySelectorAsync("h1.product-title")
                ?? await page.QuerySelectorAsync("h1[itemprop='name']");
            var name = nameEl != null ? await nameEl.TextContentAsync() ?? "" : "";

            var priceEl = await page.QuerySelectorAsync(".price") ?? await page.QuerySelectorAsync("[data-price]");
            var priceText = priceEl != null ? await priceEl.TextContentAsync() ?? "" : "";

            var brandEl = await page.QuerySelectorAsync(".product-brand") ?? await page.QuerySelectorAsync("[itemprop='brand']");
            var brand = brandEl != null ? await brandEl.TextContentAsync() ?? "" : "";

            var pidEl = await page.QuerySelectorAsync("[data-product-id]");
            var productId = pidEl != null ? await pidEl.GetAttributeAsync("data-product-id") ?? "" : "";

            await browser.CloseAsync();

            decimal qty = 1;
            decimal price = 0;
            var qtyMatch = Regex.Match(priceText, @"(\d+)");
            if (qtyMatch.Success) decimal.TryParse(qtyMatch.Value, out qty);
            var clean = Regex.Replace(priceText, "[^0-9.]", "");
            decimal.TryParse(clean, out price);

            if (string.IsNullOrWhiteSpace(name) || price == 0)
            {
                _logger.LogWarning("CigarPage incomplete {Url}", url);
                return null;
            }

            return new ScrapedProduct(
                name.Trim(), brand.Trim(), productId, price, "USD", qty,
                "CigarPage", null, null, url, ProductSource.CigarPage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CigarPage scrape failed {Url}", url);
            return null;
        }
    }

    public async Task<IReadOnlyList<string>> GetProductUrlsAsync(int count, CancellationToken ct = default)
    {
        var urls = new List<string>();
        try
        {
            var pw = await Microsoft.Playwright.Playwright.CreateAsync();
            var browser = await pw.Chromium.LaunchAsync(new() { Headless = true });
            var page = await browser.NewPageAsync();
            await page.GotoAsync(
                "https://www.cigarpage.com/cigars",
                new() { WaitUntil = WaitUntilState.NetworkIdle });
            await Task.Delay(1000, ct);
            var links = await page.QuerySelectorAllAsync("a.product-link");
            foreach (var l in links.Take(count))
            {
                var href = await l.GetAttributeAsync("href") ?? "";
                var full = href.StartsWith("http") ? href : "https://www.cigarpage.com" + href;
                if (!urls.Contains(full)) urls.Add(full);
            }
            await browser.CloseAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "CigarPage GetUrls failed");
        }
        return urls;
    }
}
