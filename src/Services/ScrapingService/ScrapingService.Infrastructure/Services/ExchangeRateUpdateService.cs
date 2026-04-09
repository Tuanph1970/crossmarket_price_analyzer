using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace ScrapingService.Infrastructure.Services;

/// <summary>
/// Updates USD to VND exchange rate from an external API and caches it.
/// </summary>
public class ExchangeRateUpdateService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IRedisCacheService _cache;
    private readonly ILogger<ExchangeRateUpdateService> _logger;

    private const string CacheKeyUsdVnd = "exchange:usd:vnd";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(1);
    private const decimal FallbackRate = 25000m; // 1 USD = 25,000 VND

    public ExchangeRateUpdateService(
        IHttpClientFactory httpClientFactory,
        IRedisCacheService cache,
        ILogger<ExchangeRateUpdateService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _cache = cache;
        _logger = logger;
    }

    public async Task<decimal> UpdateAndGetRateAsync(CancellationToken ct = default)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("ExchangeRate");
            var response = await client.GetAsync(
                "https://open.er-api.com/v6/latest/USD", ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Exchange rate API returned {StatusCode}", response.StatusCode);
                return await GetOrSetFallbackAsync(ct);
            }

            var json = await response.Content.ReadFromJsonAsync<ExchangeRateApiResponse>(ct);
            var rate = json?.Rates?.GetValueOrDefault("VND") ?? FallbackRate;

            await _cache.SetStringAsync(CacheKeyUsdVnd, rate.ToString(), CacheDuration);
            _logger.LogInformation("Exchange rate updated: 1 USD = {Rate} VND", rate);

            return rate;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch exchange rate, using fallback");
            return await GetOrSetFallbackAsync(ct);
        }
    }

    public async Task<decimal> GetCachedRateAsync(CancellationToken ct = default)
    {
        var cached = await _cache.GetStringAsync(CacheKeyUsdVnd);
        if (!string.IsNullOrEmpty(cached) && decimal.TryParse(cached, out var rate))
            return rate;
        return await UpdateAndGetRateAsync(ct);
    }

    private async Task<decimal> GetOrSetFallbackAsync(CancellationToken ct)
    {
        var cached = await _cache.GetStringAsync(CacheKeyUsdVnd);
        if (!string.IsNullOrEmpty(cached) && decimal.TryParse(cached, out var rate))
            return rate;
        await _cache.SetStringAsync(CacheKeyUsdVnd, FallbackRate.ToString(), CacheDuration);
        return FallbackRate;
    }

    private record ExchangeRateApiResponse(Dictionary<string, decimal>? Rates);
}

/// <summary>
/// Redis cache service interface (simplified for MVP — implement with StackExchange.Redis in production).
/// </summary>
public interface IRedisCacheService
{
    Task<string?> GetStringAsync(string key);
    Task SetStringAsync(string key, string value, TimeSpan? expiry = null);
}

/// <summary>
/// HTTP client factory interface.
/// </summary>
public interface IHttpClientFactory
{
    HttpClient CreateClient(string name);
}
