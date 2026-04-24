using System.Net.Http.Json;
using Common.Application.Interfaces;
using Common.Domain.Enums;
using Common.Domain.Scraping;
using Microsoft.Extensions.Logging;
using ProductService.Application.Commands;
using ProductService.Application.DTOs;
using ProductService.Application.Persistence;
using ProductService.Application.Services;
using ScoringService.Application.Services;

namespace ProductService.Application.Handlers;

/// <summary>
/// Handles QuickLookupCommand: URL → scrape → fuzzy-match VN products → score.
/// Uses local services directly (no cross-service HTTP calls):
///   - IProductScraper (via IScraperFactory)
///   - IFuzzyMatchingService
///   - IScoringEngine + ILandedCostCalculator
///   - IExchangeRateService
/// </summary>
public sealed class QuickLookupCommandHandler
    : MediatR.IRequestHandler<QuickLookupCommand, QuickLookupResultDto>
{
    private readonly IProductService _productService;
    private readonly IEnumerable<IProductScraper> _scrapers;
    private readonly IFuzzyMatchingService _fuzzyService;
    private readonly IScoringEngine _scoringEngine;
    private readonly ILandedCostCalculator _landedCostCalculator;
    private readonly IExchangeRateService _exchangeRateService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<QuickLookupCommandHandler> _logger;

    public QuickLookupCommandHandler(
        IProductService productService,
        IEnumerable<IProductScraper> scrapers,
        IFuzzyMatchingService fuzzyService,
        IScoringEngine scoringEngine,
        ILandedCostCalculator landedCostCalculator,
        IExchangeRateService exchangeRateService,
        IHttpClientFactory httpClientFactory,
        ILogger<QuickLookupCommandHandler> logger)
    {
        _productService = productService;
        _scrapers = scrapers.ToList();
        _fuzzyService = fuzzyService;
        _scoringEngine = scoringEngine;
        _landedCostCalculator = landedCostCalculator;
        _exchangeRateService = exchangeRateService;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<QuickLookupResultDto> Handle(QuickLookupCommand cmd, CancellationToken ct)
    {
        // ── Step 1: Resolve scraper ─────────────────────────────────────────────
        IProductScraper? scraper = null;
        foreach (var s in _scrapers)
        {
            if (s.CanHandle(cmd.Url)) { scraper = s; break; }
        }

        if (scraper is null)
            throw new InvalidOperationException(
                $"No scraper registered for URL: {cmd.Url}. " +
                "Supported: amazon.com, walmart.com, cigarpage.com");

        // ── Step 2: Scrape ─────────────────────────────────────────────────────
        _logger.LogInformation("QuickLookup: scraping {Url}", cmd.Url);
        var scraped = await scraper.ScrapeAsync(cmd.Url, ct);
        if (scraped is null)
            throw new InvalidOperationException($"Failed to scrape: {cmd.Url}");

        _logger.LogInformation("QuickLookup: scraped '{Name}' at {Price} {Currency}",
            scraped.Name, scraped.Price, scraped.Currency);

        // ── Step 3: Get VN candidates ───────────────────────────────────────────
        var paginatedResult = await _productService.GetProductsAsync(
            page: 1, pageSize: 200,
            source: ProductSource.Shopee,
            categoryId: null, isActive: true, ct: ct);

        var allVnProducts = paginatedResult.Items.ToList();

        if (!string.IsNullOrWhiteSpace(cmd.VnNameFilter))
        {
            var filter = cmd.VnNameFilter.ToLowerInvariant();
            allVnProducts = allVnProducts
                .Where(p => p.Name.Contains(filter, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        if (allVnProducts.Count == 0)
            _logger.LogWarning("QuickLookup: no VN (Shopee) products found");

        // ── Step 4: Fuzzy match ────────────────────────────────────────────────
        var scored = new List<(ProductListDto Vn, decimal Score)>();

        foreach (var vn in allVnProducts)
        {
            var score = _fuzzyService.ComputeMatchScore(
                scraped.Name, vn.Name, scraped.Brand, vn.BrandName);
            if (score >= cmd.MinMatchScore)
                scored.Add((vn, score));
        }

        var top = scored
            .OrderByDescending(x => x.Score)
            .Take(cmd.MaxVnMatches)
            .ToList();

        // ── Step 5: Calculate landed cost + composite score ─────────────────────
        var rate = await _exchangeRateService.GetCachedRateAsync(ct);
        const decimal DefaultShippingUsd = 10.0m;

        var scores = new List<ScoreBreakdownDto>();
        foreach (var (vn, matchScore) in top)
        {
            var priceStr = vn.LatestPrice ?? "0";
            if (!decimal.TryParse(priceStr.Replace(",", ""), out var vnPriceVnd) || vnPriceVnd <= 0)
                continue;

            var landed = _landedCostCalculator.CalculateBreakdown(
                scraped.Price, rate, DefaultShippingUsd);

            var margin = _landedCostCalculator.CalculateProfitMargin(
                vnPriceVnd, landed.TotalLandedCostVnd);

            var composite = _scoringEngine.CalculateCompositeScore(
                profitMarginPct: margin,
                demandScore: 50m,
                competitionScore: 50m,
                priceStabilityScore: 50m,
                matchConfidenceScore: matchScore);

            scores.Add(new ScoreBreakdownDto(
                MatchId: Guid.Empty,
                CompositeScore: composite,
                ProfitMarginPct: Math.Round(margin, 2),
                DemandScore: 50m,
                CompetitionScore: 50m,
                PriceStabilityScore: 50m,
                MatchConfidenceScore: matchScore,
                LandedCostVnd: landed.TotalLandedCostVnd,
                VietnamRetailVnd: vnPriceVnd,
                PriceDifferenceVnd: vnPriceVnd - landed.TotalLandedCostVnd,
                CalculatedAt: DateTime.UtcNow));
        }

        // ── Step 6: Build response ──────────────────────────────────────────────
        var scrapedDto = new ScrapedProductDto(
            scraped.Name, scraped.Brand, scraped.Sku,
            scraped.Price, scraped.Currency, scraped.SourceUrl, scraped.Source);

        var vnMatchDtos = top.Select(c => new VnMatchDto(
            c.Vn.Id, c.Vn.Name, c.Vn.BrandName,
            decimal.TryParse((c.Vn.LatestPrice ?? "0").Replace(",", ""), out var p) ? p : null,
            c.Score,
            ScoreToLevel(c.Score).ToString()
        )).ToList();

        var topScore = scores.Count > 0 ? scores.Max(s => s.CompositeScore) : 0m;
        _logger.LogInformation(
            "QuickLookup: '{Name}' → {Count} VN matches, top composite {Score:F1}",
            scraped.Name, vnMatchDtos.Count, topScore);

        // ── Step 7: Persist to DB (product → match → score) so dashboard shows results ──
        await PersistAsync(scraped, top, scores, rate, ct);

        return new QuickLookupResultDto(scrapedDto, vnMatchDtos, scores, rate, DateTime.UtcNow);
    }

    private async Task PersistAsync(
        ScrapedProduct scraped,
        List<(ProductListDto Vn, decimal Score)> top,
        List<ScoreBreakdownDto> scores,
        decimal exchangeRate,
        CancellationToken ct)
    {
        try
        {
            // 1. Save US product to ProductService DB
            var saved = await _productService.UpsertFromScrapeAsync(
                scraped.Name, scraped.Brand, scraped.Sku,
                scraped.Price, scraped.Currency, scraped.QuantityPerUnit,
                scraped.SellerName, scraped.SellerRating, scraped.SalesVolume,
                scraped.SourceUrl, scraped.Source, null, null, ct);

            if (top.Count == 0) return;

            var matchingClient = _httpClientFactory.CreateClient("MatchingService");
            var scoringClient  = _httpClientFactory.CreateClient("ScoringService");

            // 2. For each VN match: create match record → create score record
            for (int i = 0; i < top.Count; i++)
            {
                var (vn, _) = top[i];
                var scoreDto = i < scores.Count ? scores[i] : null;
                if (scoreDto == null) continue;

                // POST /api/matches
                var matchResp = await matchingClient.PostAsJsonAsync("api/matches", new
                {
                    UsProductId  = saved.Id,
                    VnProductId  = vn.Id,
                    UsProductName = scraped.Name,
                    VnProductName = vn.Name,
                    UsBrand = scraped.Brand,
                    VnBrand = vn.BrandName,
                }, ct);

                if (!matchResp.IsSuccessStatusCode)
                {
                    _logger.LogWarning("QuickLookup: match creation failed ({Status}) for {VnProduct}",
                        matchResp.StatusCode, vn.Name);
                    continue;
                }

                var matchBody = await matchResp.Content.ReadFromJsonAsync<MatchCreatedDto>(cancellationToken: ct);
                if (matchBody?.Id == null) continue;

                decimal vnPriceVnd = decimal.TryParse((vn.LatestPrice ?? "0").Replace(",", ""), out var p) ? p : 0;
                if (vnPriceVnd <= 0) continue;

                // POST /api/scores
                await scoringClient.PostAsJsonAsync("api/scores", new
                {
                    MatchId              = matchBody.Id,
                    UsPriceUsd           = scraped.Price,
                    VnRetailPriceVnd     = vnPriceVnd,
                    DemandScore          = scoreDto.DemandScore,
                    CompetitionScore     = scoreDto.CompetitionScore,
                    PriceStabilityScore  = scoreDto.PriceStabilityScore,
                    MatchConfidenceScore = scoreDto.MatchConfidenceScore,
                    ExchangeRate         = exchangeRate,
                    ShippingCostUsd      = 10.0m,
                }, ct);
            }

            _logger.LogInformation("QuickLookup: persisted product + {Count} match/score(s) for '{Name}'",
                top.Count, scraped.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "QuickLookup: persistence failed for {Url} — results still returned to caller",
                scraped.SourceUrl);
        }
    }

    private record MatchCreatedDto(Guid Id, Guid UsProductId, Guid VnProductId, decimal ConfidenceScore, string Status);

    private static ConfidenceLevel ScoreToLevel(decimal score) => score switch
    {
        >= 80m => ConfidenceLevel.High,
        >= 60m => ConfidenceLevel.Medium,
        >= 40m => ConfidenceLevel.Low,
        _ => ConfidenceLevel.Low
    };
}
