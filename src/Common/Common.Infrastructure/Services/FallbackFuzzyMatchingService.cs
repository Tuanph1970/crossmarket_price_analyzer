using Common.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Common.Infrastructure.Services;

/// <summary>
/// Stub fuzzy-matching service used by ProductService when MatchingService is unavailable.
/// Returns a simple word-overlap score — real matching is performed by MatchingService via HTTP.
/// </summary>
public sealed class FallbackFuzzyMatchingService : IFuzzyMatchingService
{
    private readonly ILogger<FallbackFuzzyMatchingService> _logger;

    public FallbackFuzzyMatchingService(ILogger<FallbackFuzzyMatchingService> logger)
        => _logger = logger;

    public decimal ComputeMatchScore(string usProductName, string vnProductName,
        string? usBrand = null, string? vnBrand = null)
    {
        _logger.LogDebug("FallbackFuzzyMatchingService: comparing '{Us}' vs '{Vn}'", usProductName, vnProductName);
        // Trivial similarity: compare lowercased name overlap
        var usWords = usProductName.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var vnWords = vnProductName.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var overlap = usWords.Intersect(vnWords).Count();
        var score = usWords.Length == 0 || vnWords.Length == 0
            ? 0m
            : Math.Min(100m, (decimal)overlap / Math.Max(usWords.Length, vnWords.Length) * 100m);
        return score;
    }
}