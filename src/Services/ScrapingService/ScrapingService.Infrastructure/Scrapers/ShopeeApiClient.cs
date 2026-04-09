using System.Text.Json;
using Common.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace ScrapingService.Infrastructure.Scrapers;

/// <summary>
/// Mock Shopee API client for MVP. Uses sample data until official API is approved.
/// For production, implement: https://open.shopee.com/docs/api/v2/marketplace/
/// </summary>
public class ShopeeApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ShopeeApiClient> _logger;
    private readonly IExchangeRateService _exchangeRateService;

    // In production, these would come from configuration
    private const string ShopeeBaseUrl = "https://shopee.vn/api/v4";
    private const int DefaultPageSize = 50;

    public ShopeeApiClient(
        HttpClient httpClient,
        ILogger<ShopeeApiClient> logger,
        IExchangeRateService exchangeRateService)
    {
        _httpClient = httpClient;
        _logger = logger;
        _exchangeRateService = exchangeRateService;
    }

    /// <summary>
    /// Search products on Shopee by keyword. Returns up to 'count' results.
    /// </summary>
    public async Task<IReadOnlyList<ShopeeProduct>> SearchProductsAsync(
        string keyword,
        int count = 50,
        CancellationToken ct = default)
    {
        // MVP: Return realistic mock data structured as Shopee API responses
        // Once API is approved, replace with actual API calls:
        // GET https://open.shopee.com/api/v4/search/search_product
        var results = GenerateMockResults(keyword, count);
        return results;
    }

    /// <summary>
    /// Get product details for a specific Shopee product by itemid/shopid.
    /// </summary>
    public async Task<ShopeeProduct?> GetProductAsync(
        long shopId,
        long itemId,
        CancellationToken ct = default)
    {
        // MVP mock: return a generated product
        return new ShopeeProduct(
            ItemId: itemId,
            ShopId: shopId,
            Name: $"Mock Product {itemId}",
            PriceVnd: (long)(Math.Round(Random.Shared.NextDouble() * 5000000) + 50000),
            Brand: null,
            Category: null,
            SellerName: "Mock Shop",
            Rating: 4.0 + Math.Round(Random.Shared.NextDouble() * 1.0, 2),
            HistoricalSold: Random.Shared.Next(0, 10000),
            SourceUrl: $"https://shopee.vn/product/{shopId}/{itemId}"
        );
    }

    private IReadOnlyList<ShopeeProduct> GenerateMockResults(string keyword, int count)
    {
        // Generate realistic mock data for MVP demonstration
        // Replace with actual API responses when Shopee API is approved
        var products = new List<ShopeeProduct>();

        for (int i = 0; i < Math.Min(count, 50); i++)
        {
            var shopId = 100000000 + i;
            var itemId = 1000000000 + i * 17;
            var basePrice = (long)(Math.Round(Random.Shared.NextDouble() * 3000000) + 50000);
            var rating = Math.Round(3.5 + Random.Shared.NextDouble() * 1.5, 2);
            var sold = Random.Shared.Next(0, 5000);

            products.Add(new ShopeeProduct(
                ItemId: itemId,
                ShopId: shopId,
                Name: $"{keyword} Premium Product #{i + 1}",
                PriceVnd: basePrice,
                Brand: i % 3 == 0 ? "Generic Brand" : null,
                Category: keyword,
                SellerName: $"Shop {keyword.Replace(" ", "")}_{i}",
                Rating: rating,
                HistoricalSold: sold,
                SourceUrl: $"https://shopee.vn/product/{shopId}/{itemId}"
            ));
        }

        _logger.LogInformation(
            "Generated {Count} mock Shopee results for keyword '{Keyword}'",
            products.Count, keyword);

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
