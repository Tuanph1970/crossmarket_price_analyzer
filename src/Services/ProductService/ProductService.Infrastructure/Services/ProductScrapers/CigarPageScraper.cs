using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Common.Domain.Enums;
using Common.Domain.Scraping;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

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
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(100) };
            var payload = new { cmd = "request.get", url, maxTimeout = 90000 };
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

    public async Task<IReadOnlyList<string>> GetProductUrlsFromPageAsync(string pageUrl, int maxCount, CancellationToken ct = default)
    {
        var products = await ScrapeListingDirectAsync(pageUrl, maxCount, ct);
        return products.Select(p => p.SourceUrl).ToList();
    }

    public async Task<IReadOnlyList<ScrapedProduct>> ScrapeListingDirectAsync(string pageUrl, int maxCount, CancellationToken ct = default)
    {
        _logger.LogInformation("CigarPage: fetching listing page {Url}", pageUrl);

        var html = await FetchViaFlareSolverr(pageUrl, ct);

        // FlareSolverr failed — fall back to Playwright stealth
        if (html is null)
        {
            _logger.LogInformation("CigarPage: falling back to Playwright stealth for listing {Url}", pageUrl);
            html = await FetchViaPlaywrightAsync(pageUrl, ct);
        }

        if (html is null)
        {
            _logger.LogWarning("CigarPage: all fetch strategies failed for listing {Url}", pageUrl);
            return Array.Empty<ScrapedProduct>();
        }

        return ParseListingHtml(html, pageUrl, maxCount);
    }

    private IReadOnlyList<ScrapedProduct> ParseListingHtml(string html, string pageUrl, int maxCount)
    {
        // Product URLs on a listing page live one level deeper than the category path.
        // e.g. listing: /samplers/best-selling-cigar-samplers.html
        //      product:  /samplers/best-selling-cigar-samplers/[slug].html
        var categoryBase = new Uri(pageUrl).AbsolutePath
            .TrimStart('/')
            .Replace(".html", "", StringComparison.OrdinalIgnoreCase)
            .TrimEnd('/') + "/";

        // Parse product-name blocks: each has an <a href="URL" title="NAME"> followed by a price span
        var productPattern = new Regex(
            @"class=""product-name[^""]*"">\s*<a[^>]*href=""([^""]+)""[^>]*title=""([^""]+)"".*?class=""price"">\$?([\d,\.]+)",
            RegexOptions.Singleline | RegexOptions.IgnoreCase);

        var results = new List<ScrapedProduct>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (Match m in productPattern.Matches(html))
        {
            if (results.Count >= maxCount) break;

            var url = m.Groups[1].Value;
            var path = new Uri(url).AbsolutePath.TrimStart('/');

            // Only accept URLs that are exactly one level under the category path
            if (!path.StartsWith(categoryBase, StringComparison.OrdinalIgnoreCase)) continue;
            var remainder = path[categoryBase.Length..];
            if (remainder.Contains('/')) continue;

            if (!seen.Add(url)) continue;

            var name = System.Net.WebUtility.HtmlDecode(m.Groups[2].Value).Trim();
            var priceStr = m.Groups[3].Value.Replace(",", "");
            if (!decimal.TryParse(priceStr, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var price) || price == 0)
                continue;

            results.Add(new ScrapedProduct(name, null, null, price, "USD", 1m,
                null, null, null, url, ProductSource.CigarPage));
        }

        _logger.LogInformation("CigarPage: extracted {Count} products from listing page", results.Count);
        return results;
    }

    private async Task<string?> FetchViaPlaywrightAsync(string url, CancellationToken ct)
    {
        try
        {
            using var pw = await Playwright.CreateAsync();
            var browser = await pw.Chromium.LaunchAsync(new()
            {
                Headless = true,
                Args = new[]
                {
                    "--no-sandbox", "--disable-setuid-sandbox", "--disable-dev-shm-usage",
                    "--disable-blink-features=AutomationControlled", "--window-size=1920,1080",
                },
            });

            var context = await browser.NewContextAsync(new()
            {
                UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36",
                ViewportSize = new ViewportSize { Width = 1920, Height = 1080 },
                ExtraHTTPHeaders = new Dictionary<string, string> { ["Accept-Language"] = "en-US,en;q=0.9" },
            });
            await context.AddInitScriptAsync(@"
                Object.defineProperty(navigator, 'webdriver', { get: () => undefined });
                Object.defineProperty(navigator, 'plugins', { get: () => [1, 2, 3, 4, 5] });
                Object.defineProperty(navigator, 'languages', { get: () => ['en-US', 'en'] });
                window.chrome = { runtime: {} };
            ");

            var page = await context.NewPageAsync();
            await page.GotoAsync(url, new() { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 45000 });
            await Task.Delay(3000, ct);

            try { await page.WaitForSelectorAsync(".product-name", new() { Timeout = 10000 }); }
            catch { }

            var html = await page.ContentAsync();
            _logger.LogInformation("CigarPage Playwright HTML snippet: {Snippet}",
                html.Length > 500 ? html[..500] : html);
            await browser.CloseAsync();
            return html;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CigarPage: Playwright fallback failed for {Url}", url);
            return null;
        }
    }
}
