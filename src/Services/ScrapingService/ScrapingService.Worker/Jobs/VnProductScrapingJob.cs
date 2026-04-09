using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Quartz;
using ScrapingService.Infrastructure.Scrapers;

namespace ScrapingService.Worker.Jobs;

/// <summary>
/// Scrapes Vietnam product data from Shopee (via mock API client for MVP).
/// Runs daily at 3am via Quartz.NET.
/// Saves scraped products to the ProductService via HTTP API.
/// </summary>
[DisallowConcurrentExecution]
public class VnProductScrapingJob : IJob
{
    private readonly ShopeeApiClient _shopeeClient;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<VnProductScrapingJob> _logger;

    private static readonly string[] SearchKeywords =
    {
        "electronics", "vitamins", "coffee", "protein", "skincare",
        "tobacco", "cigars", "beauty", "supplements", "laptops"
    };

    public VnProductScrapingJob(
        ShopeeApiClient shopeeClient,
        IHttpClientFactory httpClientFactory,
        ILogger<VnProductScrapingJob> logger)
    {
        _shopeeClient = shopeeClient;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var startedAt = DateTime.UtcNow;
        _logger.LogInformation("[{Time}] VnProductScrapingJob started", startedAt);

        var totalScraped = 0;

        foreach (var keyword in SearchKeywords)
        {
            if (context.CancellationToken.IsCancellationRequested) break;

            try
            {
                _logger.LogInformation(
                    "[{Time}] Searching Shopee for '{Keyword}'",
                    DateTime.UtcNow, keyword);

                var products = await _shopeeClient.SearchProductsAsync(
                    keyword, 50, context.CancellationToken);

                var successCount = 0;
                foreach (var product in products)
                {
                    if (context.CancellationToken.IsCancellationRequested) break;

                    try
                    {
                        await SaveProductToApiAsync(product, context.CancellationToken);
                        successCount++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to save Shopee product {Name}", product.Name);
                    }
                }

                totalScraped += successCount;
                _logger.LogInformation(
                    "[{Time}] Shopee '{Keyword}': {Count} products saved",
                    DateTime.UtcNow, keyword, successCount);

                // Rate limiting for API calls
                await Task.Delay(500, context.CancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[{Time}] Error searching Shopee for '{Keyword}'",
                    DateTime.UtcNow, keyword);
            }
        }

        _logger.LogInformation(
            "[{Time}] VnProductScrapingJob completed: {Total} total products scraped in {Duration}s",
            DateTime.UtcNow, totalScraped, (DateTime.UtcNow - startedAt).TotalSeconds);
    }

    private async Task SaveProductToApiAsync(ShopeeProduct product, CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient("ProductService");
        var payload = new
        {
            name = product.Name,
            brand = product.Brand,
            sku = $"SHOPEE-{product.ItemId}",
            price = product.PriceVnd,
            currency = "VND",
            quantityPerUnit = 1,
            sellerName = product.SellerName,
            sellerRating = product.Rating,
            salesVolume = product.HistoricalSold,
            sourceUrl = product.SourceUrl,
            source = "Shopee"
        };

        var response = await client.PostAsJsonAsync("/api/products", payload, ct);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "Failed to save Shopee product {Name}: {Status}",
                product.Name, response.StatusCode);
        }
    }
}
