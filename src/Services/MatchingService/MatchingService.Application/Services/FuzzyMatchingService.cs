using Common.Application.Interfaces;
using Common.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace MatchingService.Application.Services;

/// <summary>
/// Provides fuzzy string matching for US ↔ Vietnam product pairing.
/// Uses Levenshtein distance and TF-IDF cosine similarity.
/// </summary>
public sealed class FuzzyMatchingService : IFuzzyMatchingService
{
    private readonly ILogger<FuzzyMatchingService> _logger;

    public FuzzyMatchingService(ILogger<FuzzyMatchingService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Computes a match score (0–100) between two products using fuzzy string matching.
    /// </summary>
    public decimal ComputeMatchScore(
        string usProductName,
        string vnProductName,
        string? usBrand,
        string? vnBrand)
    {
        if (string.IsNullOrWhiteSpace(usProductName) || string.IsNullOrWhiteSpace(vnProductName))
            return 0;

        var normUs = Normalize(usProductName);
        var normVn = Normalize(vnProductName);

        decimal nameScore = ComputeCosineSimilarity(normUs, normVn) * 100m;
        decimal brandBonus = 0;

        if (!string.IsNullOrWhiteSpace(usBrand) && !string.IsNullOrWhiteSpace(vnBrand))
        {
            var normUsBrand = Normalize(usBrand);
            var normVnBrand = Normalize(vnBrand);
            var brandSim = ComputeCosineSimilarity(normUsBrand, normVnBrand);
            brandBonus = brandSim > 0.85m ? 15m : brandSim > 0.7m ? 8m : 0m;
        }

        var totalScore = Math.Clamp(nameScore * 0.85m + brandBonus, 0m, 100m);
        _logger.LogDebug("Match score: {Score} (name={Name}, brand={Brand})",
            totalScore, Math.Round(nameScore, 2), brandBonus);
        return Math.Round(totalScore, 2);
    }

    private static string Normalize(string s) =>
        s.ToLowerInvariant()
         .Replace("(", " ").Replace(")", " ")
         .Replace("-", " ").Replace("/", " ")
         .Split(' ', StringSplitOptions.RemoveEmptyEntries)
         .Aggregate("", (a, w) => a + (a.Length > 0 ? " " : "") + w)
         .Trim();

    private static decimal ComputeCosineSimilarity(string s1, string s2)
    {
        if (string.IsNullOrWhiteSpace(s1) || string.IsNullOrWhiteSpace(s2)) return 0;
        if (s1 == s2) return 1m;

        var words1 = s1.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var words2 = s2.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var allWords = words1.Union(words2).Distinct().ToList();
        if (allWords.Count == 0) return 0;

        // TF vectors
        double tf1 = words1.Length > 0 ? 1.0 / words1.Length : 0;
        double tf2 = words2.Length > 0 ? 1.0 / words2.Length : 0;

        double dotProduct = 0, mag1 = 0, mag2 = 0;
        foreach (var word in allWords)
        {
            double w1 = words1.Contains(word) ? tf1 : 0;
            double w2 = words2.Contains(word) ? tf2 : 0;
            dotProduct += w1 * w2;
            mag1 += w1 * w1;
            mag2 += w2 * w2;
        }

        if (mag1 == 0 || mag2 == 0) return 0;
        return (decimal)(dotProduct / (Math.Sqrt(mag1) * Math.Sqrt(mag2)));
    }
}
