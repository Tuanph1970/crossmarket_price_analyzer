using System.Net.Http.Json;
using System.Text.Json;
using Common.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace ProductService.Infrastructure.Services;

/// <summary>
/// Production exchange rate service — fetches USD→VND from open.er-api.com
/// and caches the result in Redis via ICacheService.
/// </summary>
public class ExchangeRateService : IExchangeRateService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ICacheService _cache;
    private readonly ILogger<ExchangeRateService> _logger;

    private const string CacheKeyUsdVnd = "exchange:usd:vnd";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(1);
    private const decimal FallbackRate = 25000m; // 1 USD = 25,000 VND

    public ExchangeRateService(
        IHttpClientFactory httpClientFactory,
        ICacheService cache,
        ILogger<ExchangeRateService> logger)
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

            await _cache.SetAsync(CacheKeyUsdVnd, new CachedRate(rate), CacheDuration);
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
        var cached = await _cache.GetAsync<CachedRate>(CacheKeyUsdVnd, ct);
        if (cached is { Rate: > 0 })
            return cached.Rate;
        return await UpdateAndGetRateAsync(ct);
    }

    private async Task<decimal> GetOrSetFallbackAsync(CancellationToken ct)
    {
        var cached = await _cache.GetAsync<CachedRate>(CacheKeyUsdVnd, ct);
        if (cached is { Rate: > 0 })
            return cached.Rate;
        await _cache.SetAsync(CacheKeyUsdVnd, new CachedRate(FallbackRate), CacheDuration);
        return FallbackRate;
    }

    // ICacheService requires class T — use this wrapper
    private record CachedRate(decimal Rate);

    private record ExchangeRateApiResponse(Dictionary<string, decimal>? Rates);
}
