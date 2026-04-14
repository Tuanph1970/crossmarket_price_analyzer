using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace ScoringService.Application.Services;

/// <summary>
/// Manages import duty (tariff) rates for Vietnam with monthly refresh capability.
/// Uses in-memory ConcurrentDictionary cache with a 30-day TTL.
/// </summary>
public class TariffService : ITariffService
{
    private readonly ConcurrentDictionary<string, TariffRate> _cache = new();
    private readonly ILogger<TariffService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private DateTime _lastRefreshed = DateTime.UtcNow;

    // Default Vietnam MFN rates for common HS code prefixes
    private static readonly Dictionary<string, decimal> VietnamDefaultRates = new()
    {
        { "8471.30", 0m },    // Computers/laptops — IT Agreement (0%)
        { "8471.40", 0m },
        { "8471.50", 0m },
        { "8471.60", 0m },
        { "8471.70", 0m },
        { "8471.80", 0m },
        { "8471.90", 0m },
        { "8517.11", 0m },    // Land-line phones
        { "8517.12", 0m },    // Mobile phones — IT Agreement (0%)
        { "8517.62", 0m },
        { "8517.70", 0m },
        { "8518.29", 0m },    // Headphones — IT Agreement (0%)
        { "8518.30", 0m },    // Headphones/earphones
        { "8525.80", 0m },    // Cameras — IT Agreement (0%)
        { "8528.72", 15m },    // Colour TV
        { "0901.11", 20m },   // Coffee beans
        { "0901.12", 10m },   // Decaffeinated / instant coffee
        { "2106.10", 10m },   // Protein supplements
        { "2106.90", 10m },   // Food supplements / vitamins
        { "2402.10", 135m },  // Cigars — highest duty
        { "2402.20", 135m },  // Cigarettes
        { "2403.10", 135m },  // Other tobacco
        { "3303.00", 30m },   // Perfume
        { "3304.91", 20m },   // Make-up
        { "3304.99", 20m },   // Skincare/cosmetics
        { "3401.30", 15m },   // Shampoo / soap
        { "6403.99", 12m },   // Footwear
        { "4202.11", 15m },   // School bags
        { "4202.91", 15m },   // Travel bags
        { "4202.99", 15m },   // Other bags
        { "8504.40", 0m },    // Chargers / power supplies — IT Agreement
        { "8507.60", 8m },    // Lithium batteries
        { "9004.10", 15m },   // Sunglasses
        { "9102.11", 10m },   // Wrist watches
    };

    public DateTime LastRefreshedAt => _lastRefreshed;

    public bool IsStale => (DateTime.UtcNow - _lastRefreshed).TotalDays > 30;

    public TariffService(ILogger<TariffService> logger, IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    /// <summary>
    /// Looks up the import duty rate for an HS code and destination country.
    /// Checks in-memory cache first, falls back to hardcoded defaults.
    /// </summary>
    public decimal GetRate(string hsCode, string destinationCountry)
    {
        var cacheKey = $"{hsCode}|{destinationCountry}";

        if (_cache.TryGetValue(cacheKey, out var cached) &&
            cached.EffectiveTo > DateTime.UtcNow)
        {
            return cached.RatePct;
        }

        // Look up in defaults by HS code prefix (most specific first)
        var rate = LookupDefaultRate(hsCode, destinationCountry);

        _logger.LogDebug("Tariff lookup {HsCode} ({Country}): {Rate}%", hsCode, destinationCountry, rate);
        return rate;
    }

    private decimal LookupDefaultRate(string hsCode, string country)
    {
        if (!country.Equals("VN", StringComparison.OrdinalIgnoreCase) &&
            !country.Equals("VNM", StringComparison.OrdinalIgnoreCase))
        {
            return 5.0m; // standard fallback for non-VN
        }

        // Try exact match first
        if (VietnamDefaultRates.TryGetValue(hsCode, out var exact))
            return exact;

        // Try first 8 digits, then 6, then 4 (progressive fallback)
        for (int len = Math.Min(8, hsCode.Length); len >= 4; len -= 2)
        {
            var prefix = hsCode[..len];
            foreach (var (key, val) in VietnamDefaultRates)
            {
                if (key.StartsWith(prefix))
                    return val;
            }
        }

        return 5.0m; // standard MFN fallback for Vietnam
    }

    /// <summary>
    /// Refreshes the tariff table from external source (or internal defaults for now).
    /// Logs and updates LastRefreshedAt regardless of outcome.
    /// </summary>
    public async Task RefreshTariffTableAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Refreshing Vietnam tariff table...");

        try
        {
            // TODO (v2): Fetch from Vietnam Customs official API:
            // var client = _httpClientFactory.CreateClient("TariffApi");
            // var rates = await client.GetFromJsonAsync<TariffApiResponse>("...", ct);
            // foreach (var r in rates.Items) { ... add to _cache ... }

            // For v1.5: just re-seed the cache from hardcoded defaults
            _cache.Clear();
            var effectiveFrom = DateTime.UtcNow;
            var effectiveTo = effectiveFrom.AddDays(90);

            foreach (var (hsCode, ratePct) in VietnamDefaultRates)
            {
                var key = $"{hsCode}|VN";
                _cache.TryAdd(key, new TariffRate(hsCode, ratePct, "VN", effectiveFrom, effectiveTo));
            }

            _lastRefreshed = DateTime.UtcNow;
            _logger.LogInformation("Tariff table refreshed: {Count} rates loaded", _cache.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh tariff table — using cached/default values");
        }

        await Task.CompletedTask;
    }
}

public interface ITariffService
{
    decimal GetRate(string hsCode, string destinationCountry);
    Task RefreshTariffTableAsync(CancellationToken ct = default);
    DateTime LastRefreshedAt { get; }
    bool IsStale { get; }
}

public record TariffRate(string HsCode, decimal RatePct, string Country, DateTime EffectiveFrom, DateTime EffectiveTo);
