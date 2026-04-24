using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Common.Domain.Enums;
using Common.Domain.Scraping;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ProductService.Infrastructure.Services.ProductScrapers;

public class CigarPageScraper : IProductScraper
{
    private readonly ILogger<CigarPageScraper> _logger;
    private readonly string _flareSolverrUrl;

    public ProductSource Source => ProductSource.CigarPage;

    public CigarPageScraper(ILogger<CigarPageScraper> logger, IConfiguration config)
    {
        _logger = logger;
        _flareSolverrUrl = config["FlareSolverr:Url"] ?? "http://localhost:8191";
    }

    public bool CanHandle(string url) =>
        url.Contains("cigarpage.com", StringComparison.OrdinalIgnoreCase);

    public async Task<ScrapedProduct?> ScrapeAsync(string url, CancellationToken ct = default)
    {
        // Extract slug from URL → search query (e.g. "arturo-fuente-hemingway-best-seller" → "arturo fuente hemingway best seller")
        var slug = ExtractSlug(url);
        var query = slug.Replace('-', ' ');
        var searchUrl = $"https://www.cigarpage.com/catalogsearch/result/?q={Uri.EscapeDataString(query)}";

        _logger.LogInformation("CigarPage: searching for '{Query}' via FlareSolverr", query);

        var html = await FetchViaFlareSolverr(searchUrl, ct);
        if (html == null)
        {
            _logger.LogWarning("FlareSolverr returned no content for {Url}", searchUrl);
            return null;
        }

        return ParseSearchResult(html, url, slug);
    }

    private ScrapedProduct? ParseSearchResult(string html, string originalUrl, string slug)
    {
        // Each product block: <span class="product-name defaultLink"><a href="URL" title="NAME">...</a></span>
        // followed by: <span class="price">$XX.XX</span>  (the second price span, without style)
        var productBlocks = Regex.Matches(html,
            @"class=""product-name defaultLink"">.*?<a\s+href=""(?<url>https://www\.cigarpage\.com[^""]+)""\s+title=""(?<name>[^""]+)"">.*?class=""price""[^>]*>[^$<]*\$(?<price1>[\d,\.]+).*?class=""price"">[\s]*\$(?<price>[\d,\.]+)",
            RegexOptions.Singleline | RegexOptions.IgnoreCase);

        // Try to find the product whose URL matches the original URL (by slug)
        foreach (Match m in productBlocks)
        {
            var productUrl = m.Groups["url"].Value;
            if (!productUrl.Contains(slug, StringComparison.OrdinalIgnoreCase)) continue;

            var name = System.Net.WebUtility.HtmlDecode(m.Groups["name"].Value).Trim();
            var priceStr = m.Groups["price"].Value.Replace(",", "");
            if (!decimal.TryParse(priceStr, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var price) || price == 0)
                continue;

            _logger.LogInformation("CigarPage search found: '{Name}' @ ${Price}", name, price);
            return new ScrapedProduct(name, "", null, price, "USD", 1m,
                null, null, null, originalUrl, ProductSource.CigarPage);
        }

        // Fallback: take first result if URL match failed
        if (productBlocks.Count > 0)
        {
            var m = productBlocks[0];
            var name = System.Net.WebUtility.HtmlDecode(m.Groups["name"].Value).Trim();
            var priceStr = m.Groups["price"].Value.Replace(",", "");
            if (decimal.TryParse(priceStr, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var price) && price > 0)
            {
                _logger.LogWarning("CigarPage: no exact URL match for {Slug}, using first result: '{Name}'", slug, name);
                return new ScrapedProduct(name, "", null, price, "USD", 1m,
                    null, null, null, originalUrl, ProductSource.CigarPage);
            }
        }

        // Simpler fallback regex — match any product-name link + next price
        var nameMatch = Regex.Match(html,
            @"href=""(?<url>https://www\.cigarpage\.com[^""]*" + Regex.Escape(slug) + @"[^""]*)""\s+title=""(?<name>[^""]+)""",
            RegexOptions.IgnoreCase);
        if (nameMatch.Success)
        {
            // Find price after this position
            var afterName = html[(nameMatch.Index + nameMatch.Length)..];
            var priceMatch = Regex.Match(afterName, @"class=""price"">\s*\$([\d,\.]+)", RegexOptions.IgnoreCase);
            if (priceMatch.Success)
            {
                var name = System.Net.WebUtility.HtmlDecode(nameMatch.Groups["name"].Value).Trim();
                var priceStr = priceMatch.Groups[1].Value.Replace(",", "");
                if (decimal.TryParse(priceStr, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var price) && price > 0)
                {
                    _logger.LogInformation("CigarPage fallback found: '{Name}' @ ${Price}", name, price);
                    return new ScrapedProduct(name, "", null, price, "USD", 1m,
                        null, null, null, originalUrl, ProductSource.CigarPage);
                }
            }
        }

        _logger.LogWarning("CigarPage: could not extract product from search results for slug '{Slug}'", slug);
        return null;
    }

    private static string ExtractSlug(string url)
    {
        // https://www.cigarpage.com/arturo-fuente-hemingway-best-seller.html → arturo-fuente-hemingway-best-seller
        var path = new Uri(url).AbsolutePath.TrimStart('/');
        return path.EndsWith(".html", StringComparison.OrdinalIgnoreCase)
            ? path[..^5]
            : path;
    }

    private async Task<string?> FetchViaFlareSolverr(string url, CancellationToken ct)
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(90) };
            var payload = new { cmd = "request.get", url, maxTimeout = 60000 };
            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            var response = await http.PostAsync($"{_flareSolverrUrl}/v1", content, ct);
            var body = await response.Content.ReadAsStringAsync(ct);

            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            if (!root.TryGetProperty("status", out var status) || status.GetString() != "ok")
            {
                _logger.LogWarning("FlareSolverr error for {Url}: {Body}", url, body[..Math.Min(300, body.Length)]);
                return null;
            }

            return root.GetProperty("solution").GetProperty("response").GetString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "FlareSolverr request failed for {Url}", url);
            return null;
        }
    }

    public Task<IReadOnlyList<string>> GetProductUrlsAsync(int count, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
}
