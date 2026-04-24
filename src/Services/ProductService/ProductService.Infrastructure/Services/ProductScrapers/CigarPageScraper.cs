using System.Text.Json;
using System.Text.RegularExpressions;
using Common.Domain.Enums;
using Common.Domain.Scraping;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace ProductService.Infrastructure.Services.ProductScrapers;

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
                Args = new[]
                {
                    "--no-sandbox",
                    "--disable-setuid-sandbox",
                    "--disable-dev-shm-usage",
                    "--disable-blink-features=AutomationControlled",
                    "--window-size=1920,1080",
                },
            });

            var context = await browser.NewContextAsync(new()
            {
                UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36",
                ViewportSize = new ViewportSize { Width = 1920, Height = 1080 },
                ExtraHTTPHeaders = new Dictionary<string, string>
                {
                    ["Accept-Language"] = "en-US,en;q=0.9",
                    ["Accept"] = "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,*/*;q=0.8",
                },
            });

            await context.AddInitScriptAsync(@"
                Object.defineProperty(navigator, 'webdriver', { get: () => undefined });
                Object.defineProperty(navigator, 'plugins', { get: () => [1, 2, 3, 4, 5] });
                Object.defineProperty(navigator, 'languages', { get: () => ['en-US', 'en'] });
                window.chrome = { runtime: {} };
            ");

            var page = await context.NewPageAsync();

            // Use NetworkIdle + long timeout to survive Cloudflare JS challenge (~5s)
            await page.GotoAsync(url, new() { WaitUntil = WaitUntilState.NetworkIdle, Timeout = 45000 });

            // Wait for Cloudflare to redirect to the real page
            await Task.Delay(5000, ct);

            // Wait for actual product content (not the Cloudflare challenge)
            try
            {
                await page.WaitForSelectorAsync(
                    "h1, [itemprop='name'], .productView-title, [data-product-title]",
                    new() { Timeout = 10000 });
            }
            catch { /* fall through and try anyway */ }

            var html = await page.ContentAsync();
            var pageTitle = await page.TitleAsync();

            // If still on Cloudflare challenge, bail
            if (pageTitle.Contains("Just a moment", StringComparison.OrdinalIgnoreCase) ||
                pageTitle.Contains("Attention Required", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("CigarPage Cloudflare challenge not cleared for {Url}", url);
                await browser.CloseAsync();
                return null;
            }

            // Try DOM selectors (BigCommerce layout)
            string name = "", brand = "", priceText = "";

            // Title selectors — BigCommerce uses these patterns
            foreach (var sel in new[] {
                "h1.productView-title", "[itemprop='name']", "h1[data-product-title]",
                ".productView-title", "h1.product-title", "h1" })
            {
                var el = await page.QuerySelectorAsync(sel);
                if (el == null) continue;
                var text = (await el.TextContentAsync() ?? "").Trim();
                if (!string.IsNullOrWhiteSpace(text)) { name = text; break; }
            }

            // Price selectors
            foreach (var sel in new[] {
                "[data-product-price]", ".price--main", ".productView-price--withoutTax",
                "[itemprop='price']", "span.price", ".price" })
            {
                var el = await page.QuerySelectorAsync(sel);
                if (el == null) continue;
                var text = (await el.TextContentAsync() ?? "").Trim();
                if (!string.IsNullOrWhiteSpace(text)) { priceText = text; break; }
            }

            // Brand selectors
            foreach (var sel in new[] {
                ".productView-brand a", "[itemprop='brand']", "span.product-brand", ".brand" })
            {
                var el = await page.QuerySelectorAsync(sel);
                if (el == null) continue;
                var text = (await el.TextContentAsync() ?? "").Trim();
                if (!string.IsNullOrWhiteSpace(text)) { brand = text; break; }
            }

            // Fall back to JSON-LD / HTML parsing if DOM selectors miss
            if (string.IsNullOrWhiteSpace(name)) name = ExtractNameFromHtml(html, pageTitle);
            if (string.IsNullOrWhiteSpace(priceText))
            {
                var p = ExtractPriceFromHtml(html);
                if (p > 0) priceText = p.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }

            await browser.CloseAsync();

            decimal price = 0;
            var clean = Regex.Replace(priceText, "[^0-9.]", "");
            decimal.TryParse(clean, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out price);

            if (string.IsNullOrWhiteSpace(name) || price == 0)
            {
                _logger.LogWarning(
                    "CigarPage scrape incomplete for {Url} — name='{Name}' price={Price} pageTitle={Title}",
                    url, name, price, pageTitle);
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

    private static string ExtractNameFromHtml(string html, string pageTitle)
    {
        // JSON-LD
        var ldMatch = Regex.Match(html,
            @"<script[^>]+type=""application/ld\+json""[^>]*>(.*?)</script>",
            RegexOptions.Singleline | RegexOptions.IgnoreCase);
        if (ldMatch.Success)
        {
            try
            {
                using var doc = JsonDocument.Parse(ldMatch.Groups[1].Value);
                if (doc.RootElement.TryGetProperty("name", out var n))
                    return n.GetString() ?? "";
            }
            catch { }
        }

        // og:title
        var ogMatch = Regex.Match(html,
            @"<meta[^>]+property=""og:title""[^>]+content=""([^""]+)""",
            RegexOptions.IgnoreCase);
        if (ogMatch.Success) return System.Net.WebUtility.HtmlDecode(ogMatch.Groups[1].Value).Trim();

        // Strip "| Cigar Page" suffix from page title
        if (!string.IsNullOrWhiteSpace(pageTitle))
        {
            var sep = pageTitle.IndexOf(" | Cigar Page", StringComparison.OrdinalIgnoreCase);
            if (sep < 0) sep = pageTitle.IndexOf(" | CigarPage", StringComparison.OrdinalIgnoreCase);
            return sep > 0 ? pageTitle[..sep].Trim() : pageTitle.Trim();
        }

        return "";
    }

    private static decimal ExtractPriceFromHtml(string html)
    {
        // JSON-LD offers
        var ldMatch = Regex.Match(html,
            @"<script[^>]+type=""application/ld\+json""[^>]*>(.*?)</script>",
            RegexOptions.Singleline | RegexOptions.IgnoreCase);
        if (ldMatch.Success)
        {
            try
            {
                using var doc = JsonDocument.Parse(ldMatch.Groups[1].Value);
                if (doc.RootElement.TryGetProperty("offers", out var offers))
                {
                    var o = offers.ValueKind == JsonValueKind.Array ? offers[0] : offers;
                    if (o.TryGetProperty("price", out var p))
                    {
                        if (p.ValueKind == JsonValueKind.Number) return p.GetDecimal();
                        if (decimal.TryParse(p.GetString(), System.Globalization.NumberStyles.Any,
                            System.Globalization.CultureInfo.InvariantCulture, out var d))
                            return d;
                    }
                }
            }
            catch { }
        }

        // og:price or meta price
        var priceMatch = Regex.Match(html,
            @"<meta[^>]+(?:property=""og:price:amount""|name=""price"")[^>]+content=""([\d.]+)""",
            RegexOptions.IgnoreCase);
        if (priceMatch.Success &&
            decimal.TryParse(priceMatch.Groups[1].Value, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var mp))
            return mp;

        // data-product-price attribute
        var dataMatch = Regex.Match(html, @"data-product-price[^>]*>([\d.]+)");
        if (dataMatch.Success &&
            decimal.TryParse(dataMatch.Groups[1].Value, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var dp))
            return dp;

        return 0;
    }

    public Task<IReadOnlyList<string>> GetProductUrlsAsync(int count, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
}
