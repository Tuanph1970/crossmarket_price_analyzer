using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Common.Domain.Enums;
using Common.Domain.Scraping;
using Microsoft.Extensions.Logging;

namespace ScrapingService.Infrastructure.Scrapers;

/// <summary>
/// Tiki.vn scraper using the undocumented REST API — no auth required.
/// API: https://tiki.vn/api/v2/products?q={keyword}&limit={n}
///      https://tiki.vn/api/v2/products/{id}
/// </summary>
public class TikiScraper : IProductScraper
{
    private readonly HttpClient _http;
    private readonly ILogger<TikiScraper> _logger;

    public ProductSource Source => ProductSource.Tiki;

    private static readonly string[] DefaultKeywords =
    {
        "xi ga", "cigar", "hop xi ga", "bo xi ga", "tau hut xi ga",
        "hop dung xi ga", "bat lua cigar", "phu kien xi ga"
    };

    public TikiScraper(HttpClient http, ILogger<TikiScraper> logger)
    {
        _http = http;
        _logger = logger;
    }

    public bool CanHandle(string url) =>
        url.Contains("tiki.vn", StringComparison.OrdinalIgnoreCase);

    // ── Public search method used by VnProductScrapingJob ──────────────────

    public async Task<IReadOnlyList<ScrapedProduct>> SearchProductsAsync(
        string keyword, int limit = 40, CancellationToken ct = default)
    {
        try
        {
            var url = $"https://tiki.vn/api/v2/products?q={Uri.EscapeDataString(keyword)}&limit={limit}&sort=top_seller&version=home-persionalized";
            var response = await _http.GetFromJsonAsync<TikiSearchResponse>(url, ct);
            if (response?.Data == null || response.Data.Count == 0)
            {
                _logger.LogInformation("Tiki search '{Keyword}': 0 results", keyword);
                return Array.Empty<ScrapedProduct>();
            }

            var results = response.Data
                .Where(p => p.Price > 0 && !string.IsNullOrWhiteSpace(p.Name))
                .Select(p => MapToScrapedProduct(p))
                .ToList();

            _logger.LogInformation("Tiki search '{Keyword}': {Count} products", keyword, results.Count);
            return results;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Tiki search failed for keyword '{Keyword}'", keyword);
            return Array.Empty<ScrapedProduct>();
        }
    }

    // ── IProductScraper: scrape single product URL ──────────────────────────

    public async Task<ScrapedProduct?> ScrapeAsync(string url, CancellationToken ct = default)
    {
        try
        {
            var productId = ExtractProductId(url);
            if (productId == null)
            {
                _logger.LogWarning("Tiki: cannot extract product ID from {Url}", url);
                return null;
            }

            var apiUrl = $"https://tiki.vn/api/v2/products/{productId}";
            var p = await _http.GetFromJsonAsync<TikiProduct>(apiUrl, ct);
            if (p == null || p.Price <= 0 || string.IsNullOrWhiteSpace(p.Name))
            {
                _logger.LogWarning("Tiki: incomplete product data for {Url}", url);
                return null;
            }

            _logger.LogInformation("Tiki: scraped '{Name}' @ {Price:N0} VND", p.Name, p.Price);
            return MapToScrapedProduct(p, url);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Tiki scrape failed for {Url}", url);
            return null;
        }
    }

    // ── IProductScraper: scrape listing/search page ─────────────────────────

    public async Task<IReadOnlyList<ScrapedProduct>> ScrapeListingDirectAsync(
        string pageUrl, int maxCount, CancellationToken ct = default)
    {
        // Extract keyword from search URL e.g. https://tiki.vn/search?q=xi+ga
        var keyword = ExtractSearchKeyword(pageUrl) ?? "xi ga";
        _logger.LogInformation("Tiki listing: scraping '{Keyword}' (max {Max})", keyword, maxCount);
        return await SearchProductsAsync(keyword, maxCount, ct);
    }

    // ── IProductScraper: collect product URLs for scheduled scraping ─────────

    public async Task<IReadOnlyList<string>> GetProductUrlsAsync(int count, CancellationToken ct = default)
    {
        var urls = new List<string>();
        foreach (var kw in DefaultKeywords)
        {
            if (urls.Count >= count) break;
            var products = await SearchProductsAsync(kw, Math.Min(20, count - urls.Count), ct);
            urls.AddRange(products.Select(p => p.SourceUrl).Where(u => !urls.Contains(u)));
            await Task.Delay(300, ct);
        }
        return urls.Take(count).ToList();
    }

    // ── Private helpers ─────────────────────────────────────────────────────

    private static ScrapedProduct MapToScrapedProduct(TikiProduct p, string? overrideUrl = null)
    {
        var sourceUrl = overrideUrl
            ?? (p.UrlPath != null
                ? $"https://tiki.vn/{p.UrlPath}"
                : $"https://tiki.vn/product/p{p.Id}.html");

        return new ScrapedProduct(
            Name: p.Name!.Trim(),
            Brand: string.IsNullOrWhiteSpace(p.BrandName) ? null : p.BrandName.Trim(),
            Sku: p.Sku ?? p.Id.ToString(),
            Price: p.Price,
            Currency: "VND",
            QuantityPerUnit: 1m,
            SellerName: p.SellerName?.Trim(),
            SellerRating: p.RatingAverage > 0 ? (decimal?)p.RatingAverage : null,
            SalesVolume: p.QuantitySold?.Value,
            SourceUrl: sourceUrl,
            Source: ProductSource.Tiki
        );
    }

    private static long? ExtractProductId(string url)
    {
        // Matches: /p123456789.html or -p123456789.html
        var match = System.Text.RegularExpressions.Regex.Match(
            url, @"[/-]p(\d{6,12})(?:\.html)?", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return match.Success && long.TryParse(match.Groups[1].Value, out var id) ? id : null;
    }

    private static string? ExtractSearchKeyword(string url)
    {
        try
        {
            var query = new Uri(url).Query.TrimStart('?');
            foreach (var part in query.Split('&'))
            {
                var kv = part.Split('=', 2);
                if (kv.Length == 2 && kv[0] == "q")
                    return Uri.UnescapeDataString(kv[1].Replace('+', ' '));
            }
            return null;
        }
        catch { return null; }
    }

    // ── JSON models ─────────────────────────────────────────────────────────

    private class TikiSearchResponse
    {
        [JsonPropertyName("data")] public List<TikiProduct>? Data { get; set; }
    }

    private class TikiProduct
    {
        [JsonPropertyName("id")]             public long Id            { get; set; }
        [JsonPropertyName("sku")]            public string? Sku        { get; set; }
        [JsonPropertyName("name")]           public string? Name       { get; set; }
        [JsonPropertyName("url_key")]        public string? UrlKey     { get; set; }
        [JsonPropertyName("url_path")]       public string? UrlPath    { get; set; }
        [JsonPropertyName("price")]          public decimal Price      { get; set; }
        [JsonPropertyName("original_price")] public decimal OriginalPrice { get; set; }
        [JsonPropertyName("brand_name")]     public string? BrandName  { get; set; }
        [JsonPropertyName("seller_name")]    public string? SellerName { get; set; }
        [JsonPropertyName("rating_average")] public decimal RatingAverage { get; set; }
        [JsonPropertyName("review_count")]   public int ReviewCount    { get; set; }
        [JsonPropertyName("quantity_sold")]  public TikiQuantitySold? QuantitySold { get; set; }
    }

    private class TikiQuantitySold
    {
        [JsonPropertyName("value")] public int? Value { get; set; }
    }
}
