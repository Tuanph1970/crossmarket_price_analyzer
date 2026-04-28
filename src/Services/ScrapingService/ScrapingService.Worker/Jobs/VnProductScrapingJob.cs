using System.Net.Http.Json;
using Common.Domain.Scraping;
using Microsoft.Extensions.Logging;
using Quartz;
using ScrapingService.Infrastructure.Scrapers;

namespace ScrapingService.Worker.Jobs;

[DisallowConcurrentExecution]
public class VnProductScrapingJob : IJob
{
    private readonly ShopeeApiClient _shopeeClient;
    private readonly LazadaApiClient _lazadaClient;
    private readonly TikiScraper _tikiScraper;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<VnProductScrapingJob> _logger;

    private static readonly string[] TikiKeywords =
    {
        "xi ga", "cigar", "hop xi ga", "bo xi ga",
        "tau hut xi ga", "phu kien xi ga", "bat lua cigar", "hop dung xi ga"
    };

    private static readonly string[] ShopeeKeywords =
    {
        "electronics", "vitamins", "coffee", "protein", "skincare",
        "tobacco", "cigars", "beauty", "supplements", "laptops"
    };

    private static readonly string[] LazadaKeywords =
    {
        "cigar", "xi ga", "tobacco", "hop xi ga", "phu kien xi ga"
    };

    public VnProductScrapingJob(
        ShopeeApiClient shopeeClient,
        LazadaApiClient lazadaClient,
        TikiScraper tikiScraper,
        IHttpClientFactory httpClientFactory,
        ILogger<VnProductScrapingJob> logger)
    {
        _shopeeClient = shopeeClient;
        _lazadaClient = lazadaClient;
        _tikiScraper = tikiScraper;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var startedAt = DateTime.UtcNow;
        _logger.LogInformation("VnProductScrapingJob started — TEMP Lazada smoke test (no saves)");

        // TEMP: Lazada-only smoke test until DOM-scraping path is confirmed.
        // Tiki + Shopee + saves are skipped here so the test isolates Lazada cleanly.
        var total = 0;
        try
        {
            var products = await _lazadaClient.SearchProductsAsync("cigar", 20, context.CancellationToken);
            _logger.LogInformation(
                "Lazada SMOKE-TEST 'cigar' → {Count} products. First 3: {Sample}",
                products.Count,
                string.Join(" || ", products.Take(3).Select(p => $"{p.Name} @ {p.PriceVnd:N0} VND")));
            total = products.Count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lazada scrape failed");
        }

        _logger.LogInformation(
            "VnProductScrapingJob completed: {Total} products in {Duration:F1}s",
            total, (DateTime.UtcNow - startedAt).TotalSeconds);
    }

    private async Task<int> ScrapeTikiAsync(CancellationToken ct)
    {
        var saved = 0;
        foreach (var keyword in TikiKeywords)
        {
            if (ct.IsCancellationRequested) break;
            try
            {
                var products = await _tikiScraper.SearchProductsAsync(keyword, 40, ct);
                foreach (var p in products)
                {
                    if (ct.IsCancellationRequested) break;
                    try { await SaveProductAsync(p, ct); saved++; }
                    catch (Exception ex) { _logger.LogWarning(ex, "Tiki save failed: {Name}", p.Name); }
                }
                _logger.LogInformation("Tiki '{Keyword}': {Count} products saved", keyword, products.Count);
                await Task.Delay(400, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Tiki scrape error for '{Keyword}'", keyword);
            }
        }
        return saved;
    }

    private async Task<int> ScrapeShopeeAsync(CancellationToken ct)
    {
        var saved = 0;
        foreach (var keyword in ShopeeKeywords)
        {
            if (ct.IsCancellationRequested) break;
            try
            {
                var products = await _shopeeClient.SearchProductsAsync(keyword, 50, ct);
                foreach (var product in products)
                {
                    if (ct.IsCancellationRequested) break;
                    try
                    {
                        await SaveShopeeProductAsync(product, ct);
                        saved++;
                    }
                    catch (Exception ex) { _logger.LogWarning(ex, "Shopee save failed: {Name}", product.Name); }
                }
                _logger.LogInformation("Shopee '{Keyword}': {Count} products saved", keyword, products.Count);
                await Task.Delay(500, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Shopee scrape error for '{Keyword}'", keyword);
            }
        }
        return saved;
    }

    private async Task SaveProductAsync(ScrapedProduct p, CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient("ProductService");
        var payload = new
        {
            name = p.Name, brand = p.Brand, sku = p.Sku,
            price = p.Price, currency = p.Currency, quantityPerUnit = (int)p.QuantityPerUnit,
            sellerName = p.SellerName, sellerRating = p.SellerRating, salesVolume = p.SalesVolume,
            sourceUrl = p.SourceUrl, source = p.Source.ToString()
        };
        var resp = await client.PostAsJsonAsync("/api/products/upsert-from-scrape", payload, ct);
        if (!resp.IsSuccessStatusCode)
            _logger.LogWarning("Save failed ({Status}) for {Name}", resp.StatusCode, p.Name);
    }

    private async Task SaveShopeeProductAsync(ShopeeProduct product, CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient("ProductService");
        var payload = new
        {
            name = product.Name, brand = product.Brand, sku = $"SHOPEE-{product.ItemId}",
            price = product.PriceVnd, currency = "VND", quantityPerUnit = 1,
            sellerName = product.SellerName, sellerRating = product.Rating,
            salesVolume = product.HistoricalSold, sourceUrl = product.SourceUrl, source = "Shopee"
        };
        var resp = await client.PostAsJsonAsync("/api/products/upsert-from-scrape", payload, ct);
        if (!resp.IsSuccessStatusCode)
            _logger.LogWarning("Shopee save failed ({Status}) for {Name}", resp.StatusCode, product.Name);
    }
}
