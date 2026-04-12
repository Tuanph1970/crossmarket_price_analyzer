using Common.Domain.Enums;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;

namespace ScrapingService.Infrastructure.Scrapers;

/// <summary>
/// Lazada Open Platform API client for v1.5 Phase 3.
/// Uses real endpoint structure; mock data is used until the Lazada API is approved.
/// Real implementation: https://open.lazada.com/doc/api-explorer
/// All HTTP calls go through a Polly-resilient HttpClient (named "Lazada").
/// </summary>
public class LazadaApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<LazadaApiClient> _logger;
    private readonly IExchangeRateService _exchangeRateService;
    private readonly ResiliencePipeline _resiliencePipeline;

    private const string LazadaBaseUrl = "https://api.lazada.com/rest";
    private const int DefaultPageSize = 50;

    public LazadaApiClient(
        HttpClient httpClient,
        ILogger<LazadaApiClient> logger,
        IExchangeRateService exchangeRateService)
    {
        _httpClient = httpClient;
        _logger = logger;
        _exchangeRateService = exchangeRateService;

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
                        "Lazada API retry {Attempt}/2", args.AttemptNumber);
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
                    _logger.LogError("Lazada API circuit breaker OPENED — breaking for {Duration}s",
                        args.BreakDuration.TotalSeconds);
                    return ValueTask.CompletedTask;
                },
            })
            .Build();
    }

    /// <summary>
    /// Search products on Lazada by keyword. Returns up to 'count' results.
    /// </summary>
    public async Task<IReadOnlyList<LazadaProduct>> SearchProductsAsync(
        string keyword,
        int count = 50,
        CancellationToken ct = default)
    {
        return await _resiliencePipeline.ExecuteAsync(async _ =>
        {
            // TODO (v1.5 Phase 3): Replace mock with real Lazada API call once approved.
            // Real endpoint: GET /search/search_product?keyword={keyword}&page_size={count}
            // Lazada API requires authentication via [Lazada App Key, App Secret, Access Token].
            // See: https://open.lazada.com/doc/api-explorer

            var results = GenerateMockResults(keyword, count);
            _logger.LogInformation(
                "Lazada search '{Keyword}': {Count} results (mock)", keyword, results.Count);
            return (IReadOnlyList<LazadaProduct>)results;
        }, ct);
    }

    /// <summary>
    /// Get product details for a specific Lazada product by item_id.
    /// </summary>
    public async Task<LazadaProduct?> GetProductAsync(
        string lazadaItemId,
        CancellationToken ct = default)
    {
        return await _resiliencePipeline.ExecuteAsync(async _ =>
        {
            // TODO (v1.5 Phase 3): Replace mock with real API call:
            // GET /product/get_item_detail?item_id={lazadaItemId}
            // Requires Authorization header with Access Token.

            var shopId = $"LS{Random.Shared.Next(100_000, 999_999)}";
            return new LazadaProduct(
                ItemId: lazadaItemId,
                ShopId: shopId,
                Name: $"Lazada Mock Product {lazadaItemId}",
                PriceVnd: (long)(Math.Round(Random.Shared.NextDouble() * 5_000_000) + 50_000),
                Brand: null,
                Category: null,
                SellerName: $"Lazada Seller #{lazadaItemId[^4..]}",
                Rating: Math.Round(3.5 + Random.Shared.NextDouble() * 1.5, 2),
                HistoricalSold: Random.Shared.Next(0, 5_000),
                SourceUrl: $"https://www.lazada.vn/products/{lazadaItemId}.html"
            );
        }, ct);
    }

    private IReadOnlyList<LazadaProduct> GenerateMockResults(string keyword, int count)
    {
        var products = new List<LazadaProduct>();
        for (int i = 0; i < Math.Min(count, 50); i++)
        {
            var itemId = $"{1000000000 + i * 13}";
            var shopId = $"LS{Random.Shared.Next(100_000, 999_999)}";
            var basePrice = (long)(Math.Round(Random.Shared.NextDouble() * 3_000_000) + 50_000);

            products.Add(new LazadaProduct(
                ItemId: itemId,
                ShopId: shopId,
                Name: $"{keyword} Premium sản phẩm #{i + 1}",
                PriceVnd: basePrice,
                Brand: i % 3 == 0 ? "Thương hiệu Chung" : null,
                Category: keyword,
                SellerName: $"Cửa hàng Lazada #{i + 1}",
                Rating: Math.Round(3.5 + Random.Shared.NextDouble() * 1.5, 2),
                HistoricalSold: Random.Shared.Next(0, 5_000),
                SourceUrl: $"https://www.lazada.vn/products/{itemId}.html"
            ));
        }
        return products;
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
