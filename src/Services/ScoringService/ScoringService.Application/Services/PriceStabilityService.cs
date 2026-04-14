using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ScoringService.Application.Persistence;

namespace ScoringService.Application.Services;

/// <summary>
/// Calculates 30-day rolling price stability scores for products.
/// Fetches price history from ProductService via HTTP, then computes
/// a coefficient-of-variation-based stability score (0-100).
/// </summary>
public class PriceStabilityService : IPriceStabilityService
{
    private readonly ScoringDbContext _db;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<PriceStabilityService> _logger;

    public PriceStabilityService(
        ScoringDbContext db,
        IHttpClientFactory httpClientFactory,
        ILogger<PriceStabilityService> logger)
    {
        _db = db;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <summary>
    /// Calculates a stability score (0–100) for a product based on 30-day price history.
    /// - CV &lt; 5%:  score = 100 (very stable)
    /// - CV &lt; 10%: score = 85
    /// - CV &lt; 15%: score = 70
    /// - CV &lt; 20%: score = 55
    /// - CV &lt; 30%: score = 40
    /// - CV ≥ 30%:  score = 25 (highly volatile)
    /// </summary>
    public async Task<decimal> CalculateStabilityScoreAsync(Guid productId, CancellationToken ct = default)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("ProductService");
            var cutoff = DateTime.UtcNow.AddDays(-30);

            var snapshots = await client
                .GetFromJsonAsync<List<PriceSnapshotDto>>(
                    $"/api/products/{productId}/price-history?from={cutoff:o}&limit=90",
                    ct) ?? [];

            if (snapshots.Count < 3)
            {
                _logger.LogDebug("Insufficient price history for product {Id} ({Count} points) — returning 50",
                    productId, snapshots.Count);
                return 50m; // medium stability, insufficient data
            }

            var prices = snapshots.Select(s => s.Price).ToArray();
            var cv = CalculateCoefficientOfVariation(prices);
            var score = MapCvToScore(cv);

            _logger.LogDebug("Product {Id}: CV={Cv:F2}%, StabilityScore={Score}", productId, cv, score);
            return score;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "PriceStabilityService failed for {Id} — returning 50", productId);
            return 50m; // graceful degradation
        }
    }

    public decimal CalculateCoefficientOfVariation(decimal[] prices)
    {
        if (prices.Length < 2) return 0m;

        var mean = prices.Average();
        if (mean == 0) return 0m;

        var sumSqDiff = prices.Sum(p => (p - mean) * (p - mean));
        var stdDev = (decimal)Math.Sqrt((double)(sumSqDiff / prices.Length));
        var cv = stdDev / mean * 100m;

        return Math.Round(cv, 4);
    }

    private static decimal MapCvToScore(decimal cv)
    {
        return cv switch
        {
            < 5m  => 100m,
            < 10m => 85m,
            < 15m => 70m,
            < 20m => 55m,
            < 30m => 40m,
            _     => 25m,
        };
    }
}

public interface IPriceStabilityService
{
    Task<decimal> CalculateStabilityScoreAsync(Guid productId, CancellationToken ct = default);
    decimal CalculateCoefficientOfVariation(decimal[] prices);
}

public record PriceSnapshotDto(Guid Id, Guid ProductId, decimal Price, string Currency, DateTime ScrapedAt);
