using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;

namespace ScrapingService.Infrastructure.Scrapers;

/// <summary>
/// Lazada Vietnam scraper using Playwright + DOM scraping.
///
/// Lazada's search page is a Single Page Application — initial SSR contains only an
/// app shell with empty JSON-LD. Product cards are rendered client-side after the SPA
/// hydrates. We use Playwright to load the page, wait for the product grid to render,
/// and extract data from the DOM directly. The grid uses `data-qa-locator="product-item"`
/// which is a stable QA-test attribute.
///
/// Anti-bot: Lazada (Alibaba) uses Baxia/FireEye for automation detection but is much
/// less aggressive than Shopee — a Playwright headless Chrome with mild stealth tweaks
/// is usually sufficient.
/// </summary>
public class LazadaApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<LazadaApiClient> _logger;
    private readonly ResiliencePipeline _resiliencePipeline;

    public LazadaApiClient(
        HttpClient httpClient,
        ILogger<LazadaApiClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

        _resiliencePipeline = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 1,
                Delay = TimeSpan.FromSeconds(2),
                BackoffType = DelayBackoffType.Exponential,
                ShouldHandle = new PredicateBuilder()
                    .Handle<Exception>(ex => ex is not OperationCanceledException),
                OnRetry = args =>
                {
                    _logger.LogWarning(args.Outcome.Exception,
                        "Lazada scrape retry {Attempt}/1", args.AttemptNumber);
                    return ValueTask.CompletedTask;
                },
            })
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                FailureRatio = 0.7,
                MinimumThroughput = 4,
                SamplingDuration = TimeSpan.FromMinutes(2),
                BreakDuration = TimeSpan.FromMinutes(5),
                ShouldHandle = new PredicateBuilder()
                    .Handle<Exception>(ex => ex is not OperationCanceledException),
                OnOpened = args =>
                {
                    _logger.LogError("Lazada circuit breaker OPENED — pausing for {Duration}min",
                        args.BreakDuration.TotalMinutes);
                    return ValueTask.CompletedTask;
                },
            })
            .Build();
    }

    public async Task<IReadOnlyList<LazadaProduct>> SearchProductsAsync(
        string keyword,
        int count = 50,
        CancellationToken ct = default)
    {
        return await _resiliencePipeline.ExecuteAsync(async token =>
        {
            var products = await ScrapeViaPlaywrightAsync(keyword, count, token);
            _logger.LogInformation("Lazada search '{Keyword}': {Count} products", keyword, products.Count);
            return (IReadOnlyList<LazadaProduct>)products;
        }, ct);
    }

    public Task<LazadaProduct?> GetProductAsync(string lazadaItemId, CancellationToken ct = default)
        => Task.FromResult<LazadaProduct?>(null);

    private async Task<List<LazadaProduct>> ScrapeViaPlaywrightAsync(
        string keyword,
        int count,
        CancellationToken ct)
    {
        var products = new List<LazadaProduct>();

        using var pw = await Playwright.CreateAsync();
        var browser = await pw.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true,
            Args = new[] { "--no-sandbox", "--disable-blink-features=AutomationControlled" }
        });

        try
        {
            var context = await browser.NewContextAsync(new BrowserNewContextOptions
            {
                UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
                            "(KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36",
                ViewportSize = new ViewportSize { Width = 1366, Height = 768 },
                Locale = "vi-VN",
                TimezoneId = "Asia/Ho_Chi_Minh",
            });

            var page = await context.NewPageAsync();
            await page.AddInitScriptAsync(
                "Object.defineProperty(navigator, 'webdriver', { get: () => undefined });");

            var responseLog = new List<string>();
            page.Response += (_, resp) =>
            {
                if (resp.Url.Contains("lazada.vn", StringComparison.OrdinalIgnoreCase) ||
                    resp.Url.Contains("alicdn.com", StringComparison.OrdinalIgnoreCase) ||
                    resp.Url.Contains("alipay", StringComparison.OrdinalIgnoreCase))
                {
                    responseLog.Add($"{resp.Status} {resp.Url[..Math.Min(120, resp.Url.Length)]}");
                }
            };

            var url = $"https://www.lazada.vn/catalog/?q={Uri.EscapeDataString(keyword)}";
            await page.GotoAsync(url, new PageGotoOptions
            {
                WaitUntil = WaitUntilState.DOMContentLoaded,
                Timeout = 45_000,
            });

            _logger.LogInformation("Lazada '{Keyword}': landed at {Final}", keyword, page.Url);

            // Wait for the product grid to appear. Try multiple known Lazada selectors.
            var gridSelectors = new[]
            {
                "[data-qa-locator='product-item']",
                "div.Bm3ON",                  // legacy/desktop card
                "div[data-tracking='product-card']",
                "a[href*='-i'][href*='.html']" // generic product link
            };

            string? matchedSelector = null;
            foreach (var sel in gridSelectors)
            {
                try
                {
                    await page.WaitForSelectorAsync(sel, new PageWaitForSelectorOptions { Timeout = 8_000 });
                    matchedSelector = sel;
                    break;
                }
                catch (TimeoutException) { /* try next */ }
            }

            if (matchedSelector is null)
            {
                var title = await page.TitleAsync();
                _logger.LogWarning(
                    "Lazada '{Keyword}': no product grid. URL={Url} Title='{Title}'. Last 6 responses: {Tail}",
                    keyword, page.Url, title,
                    string.Join(" || ", responseLog.TakeLast(6)));
                return products;
            }

            _logger.LogInformation("Lazada '{Keyword}': matched selector '{Sel}'", keyword, matchedSelector);

            var items = await page.QuerySelectorAllAsync(matchedSelector);
            foreach (var item in items)
            {
                if (products.Count >= count) break;

                var product = await ExtractProductAsync(item);
                if (product is not null) products.Add(product);
            }
        }
        finally
        {
            await browser.CloseAsync();
        }

        return products;
    }

    private async Task<LazadaProduct?> ExtractProductAsync(IElementHandle item)
    {
        try
        {
            // Title — first <a> with non-empty title attribute usually has product name
            var titleEl = await item.QuerySelectorAsync("a[title]");
            var name = titleEl is null ? null : await titleEl.GetAttributeAsync("title");
            var href = titleEl is null ? null : await titleEl.GetAttributeAsync("href");

            // Price — Lazada renders price as plain text in a price-tagged span
            var priceText = await TextOfFirstAsync(item, "span[class*='ooOxS'], .currency-value, .price, [class*='price']");

            // Brand and seller (best effort — selectors may shift)
            var brand = await TextOfFirstAsync(item, "[class*='brand'], a[class*='brand']");
            var sellerLocation = await TextOfFirstAsync(item, "[class*='location']");

            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(priceText) || string.IsNullOrWhiteSpace(href))
                return null;

            var priceVnd = ParseVndPrice(priceText);
            if (priceVnd <= 0) return null;

            var fullUrl = href!.StartsWith("http") ? href : $"https:{href}";
            var itemId = ExtractItemId(fullUrl);

            return new LazadaProduct(
                ItemId: itemId,
                ShopId: string.Empty,
                Name: name!.Trim(),
                PriceVnd: priceVnd,
                Brand: string.IsNullOrWhiteSpace(brand) ? null : brand.Trim(),
                Category: null,
                SellerName: string.IsNullOrWhiteSpace(sellerLocation) ? "Lazada Seller" : sellerLocation.Trim(),
                Rating: 0,
                HistoricalSold: 0,
                SourceUrl: fullUrl
            );
        }
        catch
        {
            return null;
        }
    }

    private static async Task<string?> TextOfFirstAsync(IElementHandle root, string selector)
    {
        var el = await root.QuerySelectorAsync(selector);
        if (el is null) return null;
        return await el.TextContentAsync();
    }

    private static long ParseVndPrice(string text)
    {
        // Examples: "₫1,250,000", "1.250.000 ₫", "1250000"
        var digits = Regex.Replace(text, @"[^\d]", "");
        return long.TryParse(digits, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : 0L;
    }

    private static string ExtractItemId(string url)
    {
        // Examples:
        //   https://www.lazada.vn/products/-i123456789-s987654321.html
        //   https://www.lazada.vn/products/something-i123456789.html
        var match = Regex.Match(url, @"-i(\d+)(?:-|\.)", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value : url.GetHashCode().ToString("X");
    }
}

public record LazadaProduct(
    string ItemId,
    string ShopId,
    string Name,
    long PriceVnd,
    string? Brand,
    string? Category,
    string SellerName,
    double Rating,
    int HistoricalSold,
    string SourceUrl
);
