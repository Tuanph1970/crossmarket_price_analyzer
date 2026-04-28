using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;

namespace ScrapingService.Infrastructure.Scrapers;

/// <summary>
/// Shopee Vietnam scraper using Playwright + response interception.
///
/// CURRENT STATUS — BLOCKED BY ANTI-BOT.
/// Shopee rejects requests at the server level (error 90309999) even when called
/// from a real headless Chrome via Playwright with stealth patches. The detection
/// is server-side: TLS fingerprint (JA3) and/or datacenter IP reputation. Local
/// stealth tweaks (`navigator.webdriver`, plugin spoofing, language locale) don't
/// help because the request never reaches a JS-evaluatable trust score.
///
/// Unblocking this requires either:
///   1. Vietnam-region residential proxies (paid; would also need IRotatingProxyService
///      to be wired through the Playwright launch),
///   2. Shopee Affiliate API (open enrollment, designed for price-comparison sites), or
///   3. Shopee Open Platform partner status (commercial registration).
///
/// The Playwright + intercept scaffolding below is left in place for option 1 and as
/// a template for other VN marketplaces with weaker anti-bot (e.g. Lazada).
///
/// Endpoint intercepted: https://shopee.vn/api/v4/search/search_items
/// </summary>
public class ShopeeApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ShopeeApiClient> _logger;
    private readonly ResiliencePipeline _resiliencePipeline;

    private const string SearchApiPath = "/api/v4/search/search_items";

    // Shopee prices come back as integers in "micro-VND" — actual VND = raw / 100_000.
    private const long ShopeePriceScale = 100_000L;

    public ShopeeApiClient(
        HttpClient httpClient,
        ILogger<ShopeeApiClient> logger)
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
                        "Shopee scrape retry {Attempt}/1", args.AttemptNumber);
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
                    _logger.LogError("Shopee circuit breaker OPENED — pausing for {Duration}min",
                        args.BreakDuration.TotalMinutes);
                    return ValueTask.CompletedTask;
                },
            })
            .Build();
    }

    public async Task<IReadOnlyList<ShopeeProduct>> SearchProductsAsync(
        string keyword,
        int count = 50,
        CancellationToken ct = default)
    {
        return await _resiliencePipeline.ExecuteAsync(async token =>
        {
            var products = await ScrapeViaPlaywrightAsync(keyword, count, token);
            _logger.LogInformation("Shopee search '{Keyword}': {Count} products", keyword, products.Count);
            return (IReadOnlyList<ShopeeProduct>)products;
        }, ct);
    }

    public async Task<ShopeeProduct?> GetProductAsync(
        long shopId,
        long itemId,
        CancellationToken ct = default)
    {
        // Single-product detail not yet implemented — search-result data is sufficient
        // for the daily scraping job. Wire up via item.get when needed.
        await Task.CompletedTask;
        return null;
    }

    private async Task<List<ShopeeProduct>> ScrapeViaPlaywrightAsync(
        string keyword,
        int count,
        CancellationToken ct)
    {
        var products = new List<ShopeeProduct>();
        var capturedJson = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);

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

            page.Response += async (_, response) =>
            {
                if (capturedJson.Task.IsCompleted) return;
                if (!response.Url.Contains(SearchApiPath, StringComparison.Ordinal)) return;

                try
                {
                    var body = await response.TextAsync();
                    capturedJson.TrySetResult(body);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to read Shopee API response body");
                    capturedJson.TrySetResult(null);
                }
            };

            var url = $"https://shopee.vn/search?keyword={Uri.EscapeDataString(keyword)}";
            await page.GotoAsync(url, new PageGotoOptions
            {
                WaitUntil = WaitUntilState.DOMContentLoaded,
                Timeout = 30_000,
            });

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(20));
            try
            {
                await Task.WhenAny(capturedJson.Task, Task.Delay(Timeout.Infinite, timeoutCts.Token));
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !ct.IsCancellationRequested) { }

            var json = capturedJson.Task.IsCompletedSuccessfully ? capturedJson.Task.Result : null;
            if (json is null) return products;

            // Detect Shopee anti-bot rejection and log it as a single concise warning.
            if (json.Contains("\"error\":90309999", StringComparison.Ordinal))
            {
                _logger.LogWarning("Shopee '{Keyword}': anti-bot rejection (error 90309999)", keyword);
                return products;
            }

            ParseSearchResponse(json, count, products);
        }
        finally
        {
            await browser.CloseAsync();
        }

        return products;
    }

    private void ParseSearchResponse(string json, int count, List<ShopeeProduct> products)
    {
        ShopeeSearchResponse? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<ShopeeSearchResponse>(json);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse Shopee search JSON (len={Len})", json.Length);
            return;
        }

        if (parsed?.Items is null || parsed.Items.Count == 0) return;

        foreach (var wrapper in parsed.Items)
        {
            if (products.Count >= count) break;
            var item = wrapper.ItemBasic;
            if (item is null || item.ItemId == 0 || string.IsNullOrWhiteSpace(item.Name)) continue;

            var priceVnd = item.Price > 0 ? item.Price / ShopeePriceScale : 0L;
            if (priceVnd <= 0) continue;

            products.Add(new ShopeeProduct(
                ItemId: item.ItemId,
                ShopId: item.ShopId,
                Name: item.Name.Trim(),
                PriceVnd: priceVnd,
                Brand: string.IsNullOrWhiteSpace(item.Brand) ? null : item.Brand.Trim(),
                Category: null,
                SellerName: item.ShopLocation ?? "Shopee Seller",
                Rating: item.ItemRating?.RatingStar ?? 0,
                HistoricalSold: item.HistoricalSold,
                SourceUrl: $"https://shopee.vn/product/{item.ShopId}/{item.ItemId}"
            ));
        }
    }

    // ── Shopee API JSON shape ────────────────────────────────────────────────

    private class ShopeeSearchResponse
    {
        [JsonPropertyName("items")] public List<ShopeeItemWrapper>? Items { get; set; }
    }

    private class ShopeeItemWrapper
    {
        [JsonPropertyName("item_basic")] public ShopeeItemBasic? ItemBasic { get; set; }
    }

    private class ShopeeItemBasic
    {
        [JsonPropertyName("itemid")]          public long ItemId { get; set; }
        [JsonPropertyName("shopid")]          public long ShopId { get; set; }
        [JsonPropertyName("name")]            public string? Name { get; set; }
        [JsonPropertyName("price")]           public long Price { get; set; }
        [JsonPropertyName("price_min")]       public long PriceMin { get; set; }
        [JsonPropertyName("brand")]           public string? Brand { get; set; }
        [JsonPropertyName("shop_location")]   public string? ShopLocation { get; set; }
        [JsonPropertyName("historical_sold")] public int HistoricalSold { get; set; }
        [JsonPropertyName("item_rating")]     public ShopeeItemRating? ItemRating { get; set; }
    }

    private class ShopeeItemRating
    {
        [JsonPropertyName("rating_star")] public double RatingStar { get; set; }
    }
}

public record ShopeeProduct(
    long ItemId,
    long ShopId,
    string Name,
    long PriceVnd,
    string? Brand,
    string? Category,
    string SellerName,
    double Rating,
    int HistoricalSold,
    string SourceUrl
);

public interface IExchangeRateService
{
    Task<decimal> UpdateAndGetRateAsync(CancellationToken ct = default);
    Task<decimal> GetCachedRateAsync(CancellationToken ct = default);
}
