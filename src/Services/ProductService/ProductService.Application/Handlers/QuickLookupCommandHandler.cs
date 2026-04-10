using Common.Application.Interfaces;
using Common.Domain.Enums;
using Common.Domain.Scraping;
using Microsoft.Extensions.Logging;
using ProductService.Application.Commands;
using ProductService.Application.DTOs;
using ProductService.Application.Persistence;
using ProductService.Application.Services;

namespace ProductService.Application.Handlers;

/// <summary>
/// Handles QuickLookupCommand: URL → scrape → fuzzy-match VN products → score.
/// Uses local services directly (no cross-service HTTP calls):
///   - IProductScraper (via IScraperFactory)
///   - FuzzyMatchingService
///   - ScoringEngine + LandedCostCalculator
///   - IExchangeRateService
/// </summary>
public class QuickLookupCommandHandler
    : MediatR.IRequestHandler<QuickLookupCommand, QuickLookupResultDto>
{
    private readonly IProductService _productService;
    private readonly IEnumerable<IProductScraper> _scrapers;
    private readonly MatchingService.Application.Services.FuzzyMatchingService _fuzzyService;
    private readonly ScoringService.Application.Services.ScoringEngine _scoringEngine;
    private readonly ScoringService.Application.Services.LandedCostCalculator _landedCostCalculator;
    private readonly IExchangeRateService _exchangeRateService;
    private readonly ILogger<QuickLookupCommandHandler> _logger;

    public QuickLookupCommandHandler(
        IProductService productService,
        IEnumerable<IProductScraper> scrapers,
        MatchingService.Application.Services.FuzzyMatchingService fuzzyService,
        ScoringService.Application.Services.ScoringEngine scoringEngine,
        ScoringService.Application.Services.LandedCostCalculator landedCostCalculator,
        IExchangeRateService exchangeRateService,
        ILogger<QuickLookupCommandHandler> logger)
    {
        _productService = productService;
        _scrapers = scrapers.ToList();
        _fuzzyService = fuzzyService;
        _scoringEngine = scoringEngine;
        _landedCostCalculator = landedCostCalculator;
        _exchangeRateService = exchangeRateService;
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

        return new QuickLookupResultDto(scrapedDto, vnMatchDtos, scores, rate, DateTime.UtcNow);
    }

    private static ConfidenceLevel ScoreToLevel(decimal score) => score switch
    {
        >= 80m => ConfidenceLevel.High,
        >= 60m => ConfidenceLevel.Medium,
        >= 40m => ConfidenceLevel.Low,
        _ => ConfidenceLevel.Low
    };
}
