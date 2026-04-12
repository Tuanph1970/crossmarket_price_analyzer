namespace Common.Application.Interfaces;

/// <summary>
/// Rotating proxy service that selects a healthy proxy using round-robin,
/// skips proxies that fail consecutively, and performs health checks.
/// </summary>
public interface IRotatingProxyService
{
    /// <summary>
    /// Returns the next healthy WebProxy for outbound HTTP calls,
    /// or null if no proxy is available.
    /// </summary>
    Task<Uri?> GetProxyAsync(CancellationToken ct = default);

    /// <summary>
    /// Reports that a proxy failed and should be marked unhealthy.
    /// </summary>
    void RecordFailure(Uri proxyUri);

    /// <summary>
    /// Number of currently healthy (unfailing) proxies.
    /// </summary>
    int HealthyCount { get; }

    /// <summary>
    /// Total number of successful proxy rotations.
    /// </summary>
    long TotalRotationCount { get; }

    /// <summary>
    /// Total number of consecutive failures across all proxies.
    /// </summary>
    long FailureCount { get; }
}
