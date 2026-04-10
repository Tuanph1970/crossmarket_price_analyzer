using Common.Domain.Enums;

namespace ProductService.Application.Commands;

/// <summary>
/// URL → scrape → match → score pipeline.
/// Takes a product URL, scrapes it, finds VN matches via FuzzyMatchingService,
/// then calculates opportunity scores via ScoringEngine.
///
/// Usage: POST /api/products/quick-lookup { Url: "https://amazon.com/..." }
/// </summary>
public record QuickLookupCommand(
    /// <summary>The product URL to look up (Amazon, Walmart, Shopee, etc.)</summary>
    string Url,

    /// <summary>
    /// Optional VN product name filter — if provided, VN match list is narrowed to products
    /// whose name contains this substring. Useful for confirming specific matches.
    /// </summary>
    string? VnNameFilter = null,

    /// <summary>
    /// Maximum number of VN match candidates to return (default 5).
    /// </summary>
    int MaxVnMatches = 5,

    /// <summary>
    /// Minimum match score threshold (0–100, default 40).
    /// VN matches below this score are excluded from results.
    /// </summary>
    decimal MinMatchScore = 40m
) : MediatR.IRequest<Application.DTOs.QuickLookupResultDto>;
