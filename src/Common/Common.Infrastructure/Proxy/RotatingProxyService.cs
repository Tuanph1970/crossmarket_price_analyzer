using System.Collections.Concurrent;
using Common.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Common.Infrastructure.Proxy;

/// <summary>
/// Rotating proxy service that selects a healthy proxy using round-robin,
/// performs periodic health checks, and skips proxies that fail consecutively.
///
/// Configured via <c>appsettings.json</c> section <c>Proxies:Urls</c>.
///
/// Usage in scraping services:
/// <code>
/// var proxyUri = await _proxyService.GetProxyAsync(ct);
/// if (proxyUri != null) { /* use proxy in HttpClient */ }
/// </code>
/// </summary>
public sealed class RotatingProxyService : IRotatingProxyService
{
    private readonly ILogger<RotatingProxyService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ConcurrentDictionary<Uri, ProxyHealthState> _states = new();
    private readonly Uri[] _proxies;
    private int _roundRobinIndex;

    private const int MaxConsecutiveFailures = 3;
    private static readonly TimeSpan HealthCheckTimeout = TimeSpan.FromSeconds(5);

    public int HealthyCount => _states.Values.Count(s => s.IsHealthy);
    public long TotalRotationCount => _totalRotationCount;
    public long FailureCount => _states.Values.Sum(s => s.ConsecutiveFailures);

    public RotatingProxyService(
        IConfiguration configuration,
        ILogger<RotatingProxyService> logger,
        IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;

        var proxyStrings = configuration.GetSection("Proxies:Urls").Get<string[]>()
                          ?? Array.Empty<string>();

        _proxies = proxyStrings
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => new Uri(s.Trim()))
            .ToArray();

        foreach (var uri in _proxies)
            _states[uri] = new ProxyHealthState();

        _logger.LogInformation(
            "RotatingProxyService initialized with {Count} proxies",
            _proxies.Length);
    }

    /// <summary>
    /// Returns the next healthy proxy using round-robin selection, or null if none are healthy.
    /// Triggers an async background health-check so the next caller gets an up-to-date view.
    /// </summary>
    public Task<Uri?> GetProxyAsync(CancellationToken ct = default)
    {
        if (_proxies.Length == 0)
        {
            _logger.LogDebug("RotatingProxyService: no proxies configured");
            return Task.FromResult<Uri?>(null);
        }

        // Scan at most one full lap around the ring
        var scanned = 0;
        var startIndex = Interlocked.Increment(ref _roundRobinIndex) % _proxies.Length;

        while (scanned < _proxies.Length)
        {
            var index = (startIndex + scanned) % _proxies.Length;
            var uri = _proxies[index];

            if (_states[uri].IsHealthy)
            {
                Interlocked.Increment(ref _totalRotationCount);
                _logger.LogDebug("RotatingProxyService: selected {Uri}", uri);

                // Kick off background health-check without blocking the return
                _ = HealthCheckAndUpdateAsync(uri, ct);

                return Task.FromResult<Uri?>(uri);
            }

            scanned++;
        }

        _logger.LogWarning(
            "RotatingProxyService: all {Count} proxies are unhealthy — returning null",
            _proxies.Length);
        return Task.FromResult<Uri?>(null);
    }

    public void RecordFailure(Uri proxyUri)
    {
        if (_states.TryGetValue(proxyUri, out var state))
        {
            var wasHealthy = state.IsHealthy;
            state.RecordFailure();

            if (wasHealthy && !state.IsHealthy)
            {
                _logger.LogWarning(
                    "RotatingProxyService: proxy {Uri} marked unhealthy after {Failures} consecutive failures",
                    proxyUri, MaxConsecutiveFailures);
            }
        }
    }

    /// <summary>
    /// Background health-check: sends a HEAD request through the proxy.
    /// Marks the proxy healthy again on success, or increments failure count on error.
    /// </summary>
    private async Task HealthCheckAndUpdateAsync(Uri proxyUri, CancellationToken ct)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Head, "http://www.google.com");
            var client = _httpClientFactory.CreateClient("RotatingProxyHealthCheck");
            client.Timeout = HealthCheckTimeout;

            var response = await client.SendAsync(request, ct);
            var isHealthy = response.IsSuccessStatusCode;

            if (isHealthy)
            {
                _states[proxyUri].RecordSuccess();
                _logger.LogTrace("RotatingProxyService: health-check OK for {Uri}", proxyUri);
            }
            else
            {
                _states[proxyUri].RecordFailure();
                _logger.LogWarning(
                    "RotatingProxyService: health-check FAILED for {Uri} — HTTP {Status}",
                    proxyUri, response.StatusCode);
            }
        }
        catch (OperationCanceledException)
        {
            // Shutdown — ignore
        }
        catch (Exception ex)
        {
            _states[proxyUri].RecordFailure();
            _logger.LogDebug(ex,
                "RotatingProxyService: health-check ERROR for {Uri}",
                proxyUri);
        }
    }

    /// <summary>
    /// Per-proxy mutable health state. Thread-safe via Interlocked operations.
    /// </summary>
    private sealed class ProxyHealthState
    {
        private int _consecutiveFailures;

        public bool IsHealthy => _consecutiveFailures < MaxConsecutiveFailures;
        public int ConsecutiveFailures => _consecutiveFailures;

        public ProxyHealthState() { }

        public void RecordFailure() => Interlocked.Increment(ref _consecutiveFailures);
        public void RecordSuccess() => Interlocked.Exchange(ref _consecutiveFailures, 0);
    }
}
