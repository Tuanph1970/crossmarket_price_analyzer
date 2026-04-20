using Common.Application.Interfaces;

namespace Common.Infrastructure.Services;

/// <summary>
/// Stub exchange rate service used when Redis is unavailable or when no real
/// exchange rate API is configured. Returns a fixed fallback rate.
/// Replace this with the full ExchangeRateService from ProductService once
/// Redis connectivity is available.
/// </summary>
public sealed class FallbackExchangeRateService : IExchangeRateService
{
    private const decimal FallbackRate = 25000m; // 1 USD = 25,000 VND

    public Task<decimal> UpdateAndGetRateAsync(CancellationToken ct = default)
        => Task.FromResult(FallbackRate);

    public Task<decimal> GetCachedRateAsync(CancellationToken ct = default)
        => Task.FromResult(FallbackRate);
}