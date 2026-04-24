using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Common.Domain.Enums;
using Common.Domain.Scraping;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace ProductService.Infrastructure.Services.ProductScrapers;

public class AmazonScraper : IProductScraper
{
    private readonly ILogger<AmazonScraper> _logger;
    public ProductSource Source => ProductSource.Amazon;

    public AmazonScraper(ILogger<AmazonScraper> logger) { _logger = logger; }

    public bool CanHandle(string url) =>
        url.Contains("amazon.com", StringComparison.OrdinalIgnoreCase);

    public async Task<ScrapedProduct?> ScrapeAsync(string url, CancellationToken ct = default)
    {
        // Primary: lightweight HTTP scrape (avoids Playwright bot-detection)
        var result = await TryScrapeViaHttpAsync(url, ct);
        if (result != null) return result;

        // Fallback: Playwright with stealth
        return await TryScrapeViaPlaywrightAsync(url, ct);
    }

    private async Task<ScrapedProduct?> TryScrapeViaHttpAsync(string url, CancellationToken ct)
    {
        try
        {
            using var handler = new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                AllowAutoRedirect = true,
            };
            using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(20) };
            client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");
            client.DefaultRequestHeaders.TryAddWithoutValidation("Accept",
                "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,*/*;q=0.8");
            client.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Language", "en-US,en;q=0.9");
            client.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Encoding", "gzip, deflate");
            client.DefaultRequestHeaders.TryAddWithoutValidation("Cache-Control", "no-cache");

            var html = await client.GetStringAsync(url, ct);

            var name = ExtractNameFromHtml(html);
            var price = ExtractPriceFromHtml(html);
            var brand = ExtractBrandFromHtml(html);
            var asin = ExtractAsin(url, html);

            if (!string.IsNullOrWhiteSpace(name) && price > 0)
            {
                _logger.LogInformation("Amazon HTTP scrape succeeded for {Url}", url);
                return new ScrapedProduct(name, brand, asin, price, "USD", 1m,
                    null, null, null, url, ProductSource.Amazon);
            }

            _logger.LogWarning("Amazon HTTP scrape incomplete for {Url} — name='{Name}' price={Price}", url, name, price);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Amazon HTTP scrape failed for {Url}, will try Playwright", url);
        }
        return null;
    }

    private static string ExtractNameFromHtml(string html)
    {
        // Try JSON-LD first
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

        // Try og:title
        var ogMatch = Regex.Match(html,
            @"<meta[^>]+property=""og:title""[^>]+content=""([^""]+)""",
            RegexOptions.IgnoreCase);
        if (ogMatch.Success)
        {
            var title = WebUtility.HtmlDecode(ogMatch.Groups[1].Value).Trim();
            // Strip " : Amazon.com: ..." suffix
            var sep = title.IndexOf(" : Amazon", StringComparison.OrdinalIgnoreCase);
            return sep > 0 ? title[..sep].Trim() : title;
        }

        // Try <title>
        var titleMatch = Regex.Match(html, @"<title>([^<]+)</title>", RegexOptions.IgnoreCase);
        if (titleMatch.Success)
        {
            var title = WebUtility.HtmlDecode(titleMatch.Groups[1].Value).Trim();
            var sep = title.IndexOf(" : Amazon", StringComparison.OrdinalIgnoreCase);
            if (sep < 0) sep = title.IndexOf(": Amazon", StringComparison.OrdinalIgnoreCase);
            return sep > 0 ? title[..sep].Trim() : title;
        }

        return "";
    }

    private static decimal ExtractPriceFromHtml(string html)
    {
        // JSON-LD price
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
                    var offersEl = offers.ValueKind == JsonValueKind.Array
                        ? offers[0] : offers;
                    if (offersEl.TryGetProperty("price", out var p))
                    {
                        if (p.ValueKind == JsonValueKind.Number)
                            return p.GetDecimal();
                        if (decimal.TryParse(p.GetString(), System.Globalization.NumberStyles.Any,
                            System.Globalization.CultureInfo.InvariantCulture, out var d))
                            return d;
                    }
                }
            }
            catch { }
        }

        // "priceAmount":"29.99" pattern in embedded JSON
        var priceAmountMatch = Regex.Match(html, @"""priceAmount""\s*:\s*""?([\d.]+)""?");
        if (priceAmountMatch.Success &&
            decimal.TryParse(priceAmountMatch.Groups[1].Value,
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var pa))
            return pa;

        // "price":"$29.99" or "price":29.99
        var priceMatch = Regex.Match(html, @"""price""\s*:\s*""?\$?([\d.,]+)""?");
        if (priceMatch.Success)
        {
            var clean = priceMatch.Groups[1].Value.Replace(",", "");
            if (decimal.TryParse(clean, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var pp))
                return pp;
        }

        // data-price or data-asin-price attributes
        var dataMatch = Regex.Match(html, @"data-(?:asin-)?price=""([\d.]+)""");
        if (dataMatch.Success &&
            decimal.TryParse(dataMatch.Groups[1].Value,
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var dp))
            return dp;

        return 0;
    }

    private static string ExtractBrandFromHtml(string html)
    {
        var ldMatch = Regex.Match(html,
            @"<script[^>]+type=""application/ld\+json""[^>]*>(.*?)</script>",
            RegexOptions.Singleline | RegexOptions.IgnoreCase);
        if (ldMatch.Success)
        {
            try
            {
                using var doc = JsonDocument.Parse(ldMatch.Groups[1].Value);
                if (doc.RootElement.TryGetProperty("brand", out var b))
                {
                    if (b.ValueKind == JsonValueKind.String) return b.GetString() ?? "";
                    if (b.TryGetProperty("name", out var bn)) return bn.GetString() ?? "";
                }
            }
            catch { }
        }
        return "";
    }

    private static string ExtractAsin(string url, string html)
    {
        // From URL: /dp/ASIN or /gp/product/ASIN
        var urlMatch = Regex.Match(url, @"(?:/dp/|/gp/product/)([A-Z0-9]{10})");
        if (urlMatch.Success) return urlMatch.Groups[1].Value;

        var htmlMatch = Regex.Match(html, @"data-asin=""([A-Z0-9]{10})""");
        return htmlMatch.Success ? htmlMatch.Groups[1].Value : "";
    }

    private async Task<ScrapedProduct?> TryScrapeViaPlaywrightAsync(string url, CancellationToken ct)
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

            try
            {
                await page.WaitForSelectorAsync(
                    "#productTitle, #captchacharacters, .a-box-inner h4",
                    new() { Timeout = 12000 });
            }
            catch { }

            var currentUrl = page.Url;
            if (currentUrl.Contains("captcha", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Amazon returned CAPTCHA for {Url}", url);
                await browser.CloseAsync();
                return null;
            }

            var nameEl = await page.QuerySelectorAsync("#productTitle");
            var brandEl = await page.QuerySelectorAsync("#bylineInfo");
            var asinEl = await page.QuerySelectorAsync("[data-asin]");

            var priceEl = await page.QuerySelectorAsync(".a-price .a-offscreen")
                       ?? await page.QuerySelectorAsync("#corePrice_feature_div .a-offscreen")
                       ?? await page.QuerySelectorAsync(".a-offscreen")
                       ?? await page.QuerySelectorAsync("#priceblock_ourprice")
                       ?? await page.QuerySelectorAsync("#priceblock_dealprice");

            var name = nameEl != null ? (await nameEl.TextContentAsync() ?? "").Trim() : "";
            var priceText = priceEl != null ? (await priceEl.TextContentAsync() ?? "").Trim() : "";
            var brand = brandEl != null ? (await brandEl.TextContentAsync() ?? "").Trim() : "";
            var asin = asinEl != null ? await asinEl.GetAttributeAsync("data-asin") ?? "" : "";

            // Also try getting data from page HTML as fallback
            if (string.IsNullOrWhiteSpace(name) || priceText == "")
            {
                var html = await page.ContentAsync();
                if (string.IsNullOrWhiteSpace(name)) name = ExtractNameFromHtml(html);
                if (string.IsNullOrWhiteSpace(priceText))
                {
                    var p = ExtractPriceFromHtml(html);
                    if (p > 0) priceText = p.ToString(System.Globalization.CultureInfo.InvariantCulture);
                }
                if (string.IsNullOrWhiteSpace(brand)) brand = ExtractBrandFromHtml(html);
                if (string.IsNullOrWhiteSpace(asin)) asin = ExtractAsin(url, html);
            }

            await browser.CloseAsync();

            decimal price = 0;
            var clean = Regex.Replace(priceText, "[^0-9.]", "");
            decimal.TryParse(clean, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out price);

            var cleanBrand = brand
                .Replace("Brand: ", "").Replace("Visit the ", "").Replace(" Store", "").Trim();

            if (string.IsNullOrWhiteSpace(name) || price == 0)
            {
                _logger.LogWarning(
                    "Amazon scrape incomplete for {Url} — name='{Name}' price={Price} pageUrl={PageUrl}",
                    url, name, price, currentUrl);
                return null;
            }

            return new ScrapedProduct(
                name, cleanBrand, asin, price, "USD", 1m,
                null, null, null, url, ProductSource.Amazon);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Amazon Playwright scrape failed for {Url}", url);
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
                var browser = await pw.Chromium.LaunchAsync(new()
                {
                    Headless = true,
                    Args = new[] { "--no-sandbox", "--disable-setuid-sandbox", "--disable-dev-shm-usage" },
                });
                var page = await browser.NewPageAsync();
                await page.GotoAsync($"https://www.amazon.com/s?k={s}",
                    new() { WaitUntil = WaitUntilState.DOMContentLoaded });
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
            catch { }
        }
        return urls;
    }
}
