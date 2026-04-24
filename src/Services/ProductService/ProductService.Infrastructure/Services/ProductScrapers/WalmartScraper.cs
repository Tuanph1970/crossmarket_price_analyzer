using System.Text.Json;
using System.Text.RegularExpressions;
using Common.Domain.Enums;
using Common.Domain.Scraping;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace ProductService.Infrastructure.Services.ProductScrapers;

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
            await page.GotoAsync(url, new() { WaitUntil = WaitUntilState.NetworkIdle, Timeout = 45000 });
            await Task.Delay(3000, ct);

            var html = await page.ContentAsync();
            await browser.CloseAsync();

            // Primary: extract from Walmart's embedded __NEXT_DATA__ JSON
            var result = ExtractFromNextData(html, url);
            if (result != null)
            {
                _logger.LogInformation("Walmart __NEXT_DATA__ scrape succeeded for {Url}", url);
                return result;
            }

            // Fallback: try DOM-based extraction from itemprop attributes in HTML
            result = ExtractFromHtml(html, url);
            if (result != null)
            {
                _logger.LogInformation("Walmart HTML scrape succeeded for {Url}", url);
                return result;
            }

            _logger.LogWarning("Walmart scrape incomplete for {Url}", url);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Walmart scrape failed for {Url}", url);
            return null;
        }
    }

    private ScrapedProduct? ExtractFromNextData(string html, string url)
    {
        var match = Regex.Match(html,
            @"<script[^>]+id=""__NEXT_DATA__""[^>]*>(.*?)</script>",
            RegexOptions.Singleline | RegexOptions.IgnoreCase);
        if (!match.Success) return null;

        try
        {
            using var doc = JsonDocument.Parse(match.Groups[1].Value);
            var root = doc.RootElement;

            // Navigate: props -> pageProps -> initialData -> data -> product -> item
            JsonElement item = default;
            if (TryNavigate(root, out item, "props", "pageProps", "initialData", "data", "product", "item") ||
                TryNavigate(root, out item, "props", "pageProps", "initialData", "data", "idml"))
            {
                var name = GetString(item, "name") ?? GetString(item, "productName") ?? "";
                var brand = GetString(item, "brand") ?? GetString(item, "brandName") ?? "";
                decimal price = 0;

                // Price is often under priceInfo -> currentPrice -> price
                if (TryNavigate(item, out var priceInfo, "priceInfo", "currentPrice"))
                    decimal.TryParse(GetString(priceInfo, "price"), System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out price);

                if (price == 0)
                    decimal.TryParse(GetString(item, "price"), System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out price);

                if (!string.IsNullOrWhiteSpace(name) && price > 0)
                    return new ScrapedProduct(name, brand, null, price, "USD", 1m,
                        null, null, null, url, ProductSource.Walmart);
            }
        }
        catch { }

        return null;
    }

    private ScrapedProduct? ExtractFromHtml(string html, string url)
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
                var name = GetString(doc.RootElement, "name") ?? "";
                var brand = "";
                if (doc.RootElement.TryGetProperty("brand", out var b))
                    brand = b.ValueKind == JsonValueKind.String ? b.GetString() ?? "" : GetString(b, "name") ?? "";
                decimal price = 0;
                if (doc.RootElement.TryGetProperty("offers", out var offers))
                {
                    var o = offers.ValueKind == JsonValueKind.Array ? offers[0] : offers;
                    if (o.TryGetProperty("price", out var p))
                    {
                        if (p.ValueKind == JsonValueKind.Number) price = p.GetDecimal();
                        else decimal.TryParse(p.GetString(), System.Globalization.NumberStyles.Any,
                            System.Globalization.CultureInfo.InvariantCulture, out price);
                    }
                }
                if (!string.IsNullOrWhiteSpace(name) && price > 0)
                    return new ScrapedProduct(name, brand, null, price, "USD", 1m,
                        null, null, null, url, ProductSource.Walmart);
            }
            catch { }
        }

        // itemprop fallback
        var nameMatch = Regex.Match(html, @"itemprop=""name""[^>]*>([^<]{3,150})<", RegexOptions.IgnoreCase);
        var priceMatch = Regex.Match(html, @"itemprop=""price""[^>]*content=""([\d.]+)""", RegexOptions.IgnoreCase);

        if (nameMatch.Success && priceMatch.Success &&
            decimal.TryParse(priceMatch.Groups[1].Value, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var price2))
        {
            return new ScrapedProduct(
                System.Net.WebUtility.HtmlDecode(nameMatch.Groups[1].Value).Trim(),
                "", null, price2, "USD", 1m, null, null, null, url, ProductSource.Walmart);
        }

        return null;
    }

    private static bool TryNavigate(JsonElement root, out JsonElement result, params string[] path)
    {
        result = root;
        foreach (var key in path)
        {
            if (result.ValueKind != JsonValueKind.Object || !result.TryGetProperty(key, out result))
                return false;
        }
        return result.ValueKind != JsonValueKind.Undefined;
    }

    private static string? GetString(JsonElement el, string key) =>
        el.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    public Task<IReadOnlyList<string>> GetProductUrlsAsync(int count, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
}
