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
                    "--disable-infobars",
                    "--window-size=1920,1080",
                    "--disable-web-security",
                    "--allow-running-insecure-content",
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

            // Stealth: mask automation indicators
            await context.AddInitScriptAsync(@"
                Object.defineProperty(navigator, 'webdriver', { get: () => undefined });
                Object.defineProperty(navigator, 'plugins', { get: () => [1, 2, 3, 4, 5] });
                Object.defineProperty(navigator, 'languages', { get: () => ['en-US', 'en'] });
                window.chrome = { runtime: {} };
            ");

            var page = await context.NewPageAsync();
            await page.GotoAsync(url, new() { WaitUntil = WaitUntilState.NetworkIdle, Timeout = 45000 });

            // Wait for product title or a bot-check indicator (whichever arrives first)
            try
            {
                await page.WaitForSelectorAsync(
                    "#productTitle, #captchacharacters, .a-box-inner h4",
                    new() { Timeout = 12000 });
            }
            catch { /* timeout is fine — fall through to selector reads */ }

            var currentUrl = page.Url;
            if (currentUrl.Contains("captcha", StringComparison.OrdinalIgnoreCase) ||
                currentUrl.Contains("validateCaptcha", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Amazon returned CAPTCHA for {Url}", url);
                await browser.CloseAsync();
                return null;
            }

            var nameEl = await page.QuerySelectorAsync("#productTitle");
            var brandEl = await page.QuerySelectorAsync("#bylineInfo");
            var asinEl = await page.QuerySelectorAsync("[data-asin]");

            // Try multiple price selectors — Amazon layout varies by product/region
            var priceEl = await page.QuerySelectorAsync(".a-price .a-offscreen")
                       ?? await page.QuerySelectorAsync("#corePrice_feature_div .a-offscreen")
                       ?? await page.QuerySelectorAsync(".a-offscreen")
                       ?? await page.QuerySelectorAsync("#priceblock_ourprice")
                       ?? await page.QuerySelectorAsync("#priceblock_dealprice")
                       ?? await page.QuerySelectorAsync("#apex_offerDisplay_desktop .a-offscreen");

            var name = nameEl != null ? (await nameEl.TextContentAsync() ?? "").Trim() : "";
            var priceText = priceEl != null ? (await priceEl.TextContentAsync() ?? "").Trim() : "";
            var brand = brandEl != null ? (await brandEl.TextContentAsync() ?? "").Trim() : "";
            var asin = asinEl != null ? await asinEl.GetAttributeAsync("data-asin") ?? "" : "";

            await browser.CloseAsync();

            decimal price = 0;
            var clean = Regex.Replace(priceText, "[^0-9.]", "");
            decimal.TryParse(clean, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out price);

            var cleanBrand = brand
                .Replace("Brand: ", "")
                .Replace("Visit the ", "")
                .Replace(" Store", "")
                .Trim();

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
            catch { /* skip individual failures */ }
        }
        return urls;
    }
}
