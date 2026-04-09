using System.Net.Http.Json;
using Common.Domain.Enums;
using Microsoft.Extensions.Logging;
using Quartz;
using ScrapingService.Infrastructure.Scrapers;

namespace ScrapingService.Worker.Jobs;

/// <summary>
/// Scrapes U.S. product data from Amazon, Walmart, and CigarPage.
/// Runs daily at 2am via Quartz.NET.
/// Saves scraped products to the ProductService via HTTP API.
/// </summary>
[DisallowConcurrentExecution]
public class UsProductScrapingJob : IJob
{
    private readonly IEnumerable<IProductScraper> _scrapers;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<UsProductScrapingJob> _logger;

    private const int ProductsPerSource = 50;

    public UsProductScrapingJob(
        IEnumerable<IProductScraper> scrapers,
        IHttpClientFactory httpClientFactory,
        ILogger<UsProductScrapingJob> logger)
    {
        _scrapers = scrapers;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var startedAt = DateTime.UtcNow;
        _logger.LogInformation("[{Time}] UsProductScrapingJob started", startedAt);

        var usScrapers = _scrapers
            .Where(s => s.Source is ProductSource.Amazon or ProductSource.Walmart or ProductSource.CigarPage)
            .ToList();

        if (usScrapers.Count == 0)
        {
            _logger.LogWarning("No U.S. scrapers registered, skipping job");
            return;
        }

        foreach (var scraper in usScrapers)
        {
            try
            {
                _logger.LogInformation(
                    "[{Time}] Scraping products from {Source}",
                    DateTime.UtcNow, scraper.Source);

                var urls = await scraper.GetProductUrlsAsync(ProductsPerSource, context.CancellationToken);
                _logger.LogInformation("Found {Count} URLs from {Source}", urls.Count, scraper.Source);

                var successCount = 0;
                var failCount = 0;

                foreach (var url in urls)
                {
                    if (context.CancellationToken.IsCancellationRequested) break;

                    try
                    {
                        var product = await scraper.ScrapeAsync(url, context.CancellationToken);
                        if (product != null)
                        {
                            await SaveProductToApiAsync(product, context.CancellationToken);
                            successCount++;
                        }
                        else
                        {
                            failCount++;
                        }

                        // Rate limiting: pause between requests
                        await Task.Delay(2000, context.CancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to scrape {Url}", url);
                        failCount++;
                    }
                }

                _logger.LogInformation(
                    "[{Time}] {Source} scraping completed: {Success} saved, {Failed} failed",
                    DateTime.UtcNow, scraper.Source, successCount, failCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[{Time}] Error scraping from {Source}", DateTime.UtcNow, scraper.Source);
            }
        }

        _logger.LogInformation(
            "[{Time}] UsProductScrapingJob completed in {Duration}s",
            DateTime.UtcNow, (DateTime.UtcNow - startedAt).TotalSeconds);
    }

    private async Task SaveProductToApiAsync(ScrapedProduct product, CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient("ProductService");
        var payload = new
        {
            name = product.Name,
            brand = product.Brand,
            sku = product.Sku,
            price = product.Price,
            currency = product.Currency,
            quantityPerUnit = product.QuantityPerUnit,
            sellerName = product.SellerName,
            sellerRating = product.SellerRating,
            salesVolume = product.SalesVolume,
            sourceUrl = product.SourceUrl,
            source = product.Source.ToString()
        };

        var response = await client.PostAsJsonAsync("/api/products", payload, ct);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "Failed to save product {Name} to API: {Status}",
                product.Name, response.StatusCode);
        }
    }
}
