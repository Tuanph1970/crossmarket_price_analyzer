using Common.Domain.Enums;

namespace ProductService.Application.DTOs;

/// <summary>
/// Result of a QuickLookup: scraped US product + top VN matches + scoring.
/// </summary>
public record QuickLookupResultDto(
    ScrapedProductDto ScrapedProduct,
    IReadOnlyList<VnMatchDto> VnMatches,
    IReadOnlyList<ScoreBreakdownDto> Scores,
    decimal ExchangeRate,
    DateTime LookedUpAt
);

public record ScrapedProductDto(
    string Name,
    string? Brand,
    string? Sku,
    decimal Price,
    string Currency,
    string SourceUrl,
    ProductSource Source
);

public record VnMatchDto(
    Guid ProductId,
    string Name,
    string? BrandName,
    decimal? LatestPriceVnd,
    decimal MatchScore,
    string MatchConfidenceLevel
);

public record ScrapeListingResultDto(
    string PageUrl,
    IReadOnlyList<ScrapedProductDto> Products,
    int TotalFound,
    DateTime ScrapedAt
);

public record ScoreBreakdownDto(
    Guid MatchId,
    decimal CompositeScore,
    decimal ProfitMarginPct,
    decimal DemandScore,
    decimal CompetitionScore,
    decimal PriceStabilityScore,
    decimal MatchConfidenceScore,
    decimal LandedCostVnd,
    decimal VietnamRetailVnd,
    decimal PriceDifferenceVnd,
    DateTime CalculatedAt
);
