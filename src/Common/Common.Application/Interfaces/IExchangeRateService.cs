namespace Common.Application.Interfaces;

/// <summary>
/// Exchange rate service contract available to all microservices.
/// </summary>
public interface IExchangeRateService
{
    /// <summary>
    /// Fetches the latest USD → VND rate from the external API and caches it.
    /// </summary>
    Task<decimal> UpdateAndGetRateAsync(CancellationToken ct = default);

    /// <summary>
    /// Returns the cached rate if available, otherwise fetches and caches a new rate.
    /// </summary>
    Task<decimal> GetCachedRateAsync(CancellationToken ct = default);
}
