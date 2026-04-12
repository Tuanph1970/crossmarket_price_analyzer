using System.Text.Json;
using Common.Domain.Enums;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;

namespace ScrapingService.Infrastructure.Scrapers;

/// <summary>
/// Shopee API client for MVP. Uses sample data until official API is approved.
/// Real implementation: https://open.shopee.com/docs/api/v2/marketplace/
/// All HTTP calls go through a Polly-resilient HttpClient (named "Shopee").
/// </summary>
public class ShopeeApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ShopeeApiClient> _logger;
    private readonly IExchangeRateService _exchangeRateService;
    private readonly ResiliencePipeline _resiliencePipeline;

    private const string ShopeeBaseUrl = "https://open.shopee.com/api/v4";
    private const int DefaultPageSize = 50;

    public ShopeeApiClient(
        HttpClient httpClient,
        ILogger<ShopeeApiClient> logger,
        IExchangeRateService exchangeRateService)
    {
        _httpClient = httpClient;
        _logger = logger;
        _exchangeRateService = exchangeRateService;

        // Per-client resilience: retry + circuit breaker on top of the named HttpClient policy
        _resiliencePipeline = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 2,
                Delay = TimeSpan.FromSeconds(1),
                BackoffType = DelayBackoffType.Exponential,
                ShouldHandle = new PredicateBuilder()
                    .Handle<Exception>(ex => ex is not OperationCanceledException),
                OnRetry = args =>
                {
                    _logger.LogWarning(args.Outcome.Exception,
                        "Shopee API retry {Attempt}/2", args.AttemptNumber);
                    return ValueTask.CompletedTask;
                },
            })
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                FailureRatio = 0.5,
                MinimumThroughput = 5,
                SamplingDuration = TimeSpan.FromSeconds(30),
                BreakDuration = TimeSpan.FromSeconds(60),
                ShouldHandle = new PredicateBuilder()
                    .Handle<Exception>(ex => ex is not OperationCanceledException),
                OnOpened = args =>
                {
                    _logger.LogError("Shopee API circuit breaker OPENED — breaking for {Duration}s",
                        args.BreakDuration.TotalSeconds);
                    return ValueTask.CompletedTask;
                },
            })
            .Build();
    }

    /// <summary>
    /// Search products on Shopee by keyword. Returns up to 'count' results.
    /// </summary>
    public async Task<IReadOnlyList<ShopeeProduct>> SearchProductsAsync(
        string keyword,
        int count = 50,
        CancellationToken ct = default)
    {
        return await _resiliencePipeline.ExecuteAsync(async token =>
        {
            // TODO (v1.5): Replace mock with real API call once Shopee API is approved:
            // var response = await _httpClient.GetAsync(
            //     $"{ShopeeBaseUrl}/search/search_product?keyword={Uri.EscapeDataString(keyword)}&page_size={count}",
            //     token);
            // var json = await response.Content.ReadFromJsonAsync<ShopeeSearchResponse>(token);
            // return MapToShopeeProducts(json);

            var results = GenerateMockResults(keyword, count);
            _logger.LogInformation(
                "Shopee search '{Keyword}': {Count} results (mock)", keyword, results.Count);
            return (IReadOnlyList<ShopeeProduct>)results;
        }, ct);
    }

    /// <summary>
    /// Get product details for a specific Shopee product by itemid/shopid.
    /// </summary>
    public async Task<ShopeeProduct?> GetProductAsync(
        long shopId,
        long itemId,
        CancellationToken ct = default)
    {
        return await _resiliencePipeline.ExecuteAsync(async token =>
        {
            // TODO (v1.5): Replace mock with real API call:
            // var response = await _httpClient.GetAsync(
            //     $"{ShopeeBaseUrl}/product/get_item_detail?shopid={shopId}&itemid={itemId}", token);

            return new ShopeeProduct(
                ItemId: itemId, ShopId: shopId,
                Name: $"Mock Product {itemId}",
                PriceVnd: (long)(Math.Round(Random.Shared.NextDouble() * 5_000_000) + 50_000),
                Brand: null, Category: null,
                SellerName: "Mock Shop",
                Rating: Math.Round(4.0 + Random.Shared.NextDouble(), 2),
                HistoricalSold: Random.Shared.Next(0, 10_000),
                SourceUrl: $"https://shopee.vn/product/{shopId}/{itemId}"
            );
        }, ct);
    }

    private IReadOnlyList<ShopeeProduct> GenerateMockResults(string keyword, int count)
    {
        var products = new List<ShopeeProduct>();
        for (int i = 0; i < Math.Min(count, 50); i++)
        {
            var shopId = 100_000_000L + i;
            var itemId = 1_000_000_000L + i * 17;
            var basePrice = (long)(Math.Round(Random.Shared.NextDouble() * 3_000_000) + 50_000);

            products.Add(new ShopeeProduct(
                ItemId: itemId, ShopId: shopId,
                Name: $"{keyword} Premium Product #{i + 1}",
                PriceVnd: basePrice,
                Brand: i % 3 == 0 ? "Generic Brand" : null,
                Category: keyword,
                SellerName: $"Shop {keyword.Replace(" ", "")}_{i}",
                Rating: Math.Round(3.5 + Random.Shared.NextDouble() * 1.5, 2),
                HistoricalSold: Random.Shared.Next(0, 5_000),
                SourceUrl: $"https://shopee.vn/product/{shopId}/{itemId}"
            ));
        }
        return products;
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
