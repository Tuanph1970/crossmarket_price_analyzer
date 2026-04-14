using System.Text.RegularExpressions;
using Common.Domain.Enums;
using Common.Domain.Scraping;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace ScrapingService.Infrastructure.Scrapers;

/// <summary>
/// Tiki.vn product scraper using Microsoft Playwright.
/// Falls back gracefully on any failure — logs warning and returns null.
/// </summary>
public class TikiScraper : IProductScraper
{
    private readonly ILogger<TikiScraper> _logger;
    public ProductSource Source => ProductSource.Tiki;

    public TikiScraper(ILogger<TikiScraper> logger) => _logger = logger;

    public bool CanHandle(string url) =>
        url.Contains("tiki.vn", StringComparison.OrdinalIgnoreCase);

    public async Task<ScrapedProduct?> ScrapeAsync(string url, CancellationToken ct = default)
    {
        IBrowser? browser = null;
        try
        {
            var pw = await Playwright.CreateAsync();
            browser = await pw.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
            var page = await browser.NewPageAsync();

            await page.GotoAsync(url, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
            await Task.Delay(2000, ct); // allow JS to render dynamic content

            // Name: .product-title (primary), h1.title (fallback)
            string? name = null;
            var nameEl = await page.QuerySelectorAsync(".product-title");
            if (nameEl != null) name = await nameEl.TextContentAsync();
            if (string.IsNullOrWhiteSpace(name))
            {
                var titleEl = await page.QuerySelectorAsync("h1.title");
                name = titleEl != null ? await titleEl.TextContentAsync() : null;
            }

            // Price: .product-price__current-price (current), .price-discount (fallback)
            long priceVnd = 0;
            var priceEl = await page.QuerySelectorAsync(".product-price__current-price")
                         ?? await page.QuerySelectorAsync(".price-discount");
            if (priceEl != null)
            {
                var priceText = await priceEl.TextContentAsync() ?? "";
                var clean = Regex.Replace(priceText, "[^0-9]", "");
                long.TryParse(clean, out priceVnd);
            }

            // Brand: .brand-link (primary), #brand (fallback)
            string? brand = null;
            var brandEl = await page.QuerySelectorAsync(".brand-link")
                         ?? await page.QuerySelectorAsync("#brand");
            if (brandEl != null) brand = await brandEl.TextContentAsync();

            // Rating: .rating-review__stars — value in aria-label (e.g. "4.5 sao")
            double? rating = null;
            var ratingEl = await page.QuerySelectorAsync(".rating-review__stars");
            if (ratingEl != null)
            {
                var ariaLabel = await ratingEl.GetAttributeAsync("aria-label") ?? "";
                var match = Regex.Match(ariaLabel, @"([0-9.,]+)\s*sao");
                if (match.Success && double.TryParse(match.Groups[1].Value, out var r))
                    rating = r;
            }

            // Seller: .seller-info__name
            string? seller = null;
            var sellerEl = await page.QuerySelectorAsync(".seller-info__name");
            if (sellerEl != null) seller = await sellerEl.TextContentAsync();

            // External ID from URL path (e.g. /p/abc-123.html → abc-123)
            string? externalId = null;
            var segments = new Uri(url).AbsolutePath.Trim('/').Split('/');
            if (segments.Length > 0)
                externalId = segments[^1].Replace(".html", "");

            await browser.CloseAsync();

            if (string.IsNullOrWhiteSpace(name) || priceVnd == 0)
            {
                _logger.LogWarning("Tiki incomplete scrape result for {Url}", url);
                return null;
            }

            return new ScrapedProduct(
                Name: name.Trim(),
                Brand: string.IsNullOrWhiteSpace(brand) ? null : brand.Trim(),
                Sku: externalId ?? "",
                Price: priceVnd,
                Currency: "VND",
                QuantityPerUnit: 1m,
                SellerName: seller?.Trim(),
                SellerRating: rating.HasValue ? (decimal)rating.Value : null,
                SalesVolume: null,
                SourceUrl: url,
                Source: ProductSource.Tiki
            );
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Tiki scrape cancelled for {Url}", url);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Tiki scrape failed for {Url}", url);
            return null;
        }
        finally
        {
            if (browser != null)
            {
                try { await browser.CloseAsync(); }
                catch { /* best-effort cleanup */ }
            }
        }
    }

    public async Task<IReadOnlyList<string>> GetProductUrlsAsync(int count, CancellationToken ct = default)
    {
        var urls = new List<string>();
        foreach (var keyword in new[] { "laptop", "smartphone", "vitamin" })
        {
            if (urls.Count >= count) break;
            try
            {
                var pw = await Playwright.CreateAsync();
                var browser = await pw.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
                var page = await browser.NewPageAsync();

                var searchUrl = $"https://tiki.vn/search?q={Uri.EscapeDataString(keyword)}";
                await page.GotoAsync(searchUrl, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
                await Task.Delay(2000, ct);

                var links = await page.QuerySelectorAllAsync("a.product-item");
                foreach (var link in links.Take(count - urls.Count))
                {
                    var href = await link.GetAttributeAsync("href") ?? "";
                    if (!string.IsNullOrWhiteSpace(href))
                    {
                        var full = href.StartsWith("http") ? href : "https://tiki.vn" + href;
                        if (!urls.Contains(full)) urls.Add(full);
                    }
                }
                await browser.CloseAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Tiki URL collection failed for keyword '{Keyword}'", keyword);
            }
        }
        return urls;
    }
}
