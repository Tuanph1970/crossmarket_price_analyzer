using Common.Application.Interfaces;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;

namespace ScoringService.Application.Services;

/// <summary>
/// Unified shipping service integrating FedEx (primary) and DHL (fallback) APIs.
/// Currently uses mock data — TODO: replace with real FedEx/DHL API calls once credentials are provisioned.
/// Real FedEx API: https://www.fedex.com/en-us/developer/web-services.html
/// Real DHL API: https://www.dhl.com/global-en/home/products/api-integration.html
/// </summary>
public class ShippingService : IShippingService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ShippingService> _logger;
    private readonly IExchangeRateService _exchangeRateService;
    private readonly ResiliencePipeline _resiliencePipeline;

    public ShippingService(
        HttpClient httpClient,
        ILogger<ShippingService> logger,
        IExchangeRateService exchangeRateService)
    {
        _httpClient = httpClient;
        _logger = logger;
        _exchangeRateService = exchangeRateService;

        _resiliencePipeline = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 2,
                Delay = TimeSpan.FromSeconds(1),
                BackoffType = DelayBackoffType.Exponential,
                ShouldHandle = new PredicateBuilder()
                    .Handle<Exception>(ex => ex is not OperationCanceledException),
                OnRetry = args =>
                {
                    _logger.LogWarning(args.Outcome.Exception,
                        "ShippingService retry {Attempt}/2", args.AttemptNumber);
                    return ValueTask.CompletedTask;
                },
            })
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                FailureRatio = 0.5,
                MinimumThroughput = 5,
                SamplingDuration = TimeSpan.FromSeconds(30),
                BreakDuration = TimeSpan.FromSeconds(60),
                ShouldHandle = new PredicateBuilder()
                    .Handle<Exception>(ex => ex is not OperationCanceledException),
                OnOpened = args =>
                {
                    _logger.LogError("ShippingService circuit breaker OPENED — breaking for {Duration}s",
                        args.BreakDuration.TotalSeconds);
                    return ValueTask.CompletedTask;
                },
            })
            .Build();
    }

    /// <summary>
    /// Returns a quote from the fastest/cheapest available carrier.
    /// Tries FedEx first; falls back to DHL if FedEx fails.
    /// </summary>
    public async Task<ShippingQuote?> GetQuoteAsync(ShippingRequest request, CancellationToken ct = default)
    {
        try
        {
            var fedexQuote = await GetFedExQuoteAsync(request, ct);
            if (fedexQuote is not null)
            {
                _logger.LogInformation(
                    "FedEx quote for {Weight}kg: ${Rate} ({Days} days)",
                    request.WeightKg, fedexQuote.RateUsd, fedexQuote.EstimatedDays);
                return fedexQuote;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "FedEx quote failed, falling back to DHL");
        }

        try
        {
            var dhlQuote = await GetDhlQuoteAsync(request, ct);
            if (dhlQuote is not null)
            {
                _logger.LogInformation(
                    "DHL quote for {Weight}kg: ${Rate} ({Days} days)",
                    request.WeightKg, dhlQuote.RateUsd, dhlQuote.EstimatedDays);
                return dhlQuote;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DHL quote also failed for {Weight}kg package", request.WeightKg);
        }

        _logger.LogWarning("No shipping quote available for {Weight}kg from {Origin}→{Dest}",
            request.WeightKg, request.OriginCountryCode, request.DestinationCountryCode);
        return null;
    }

    /// <summary>
    /// Returns all available carrier quotes in parallel.
    /// Only carriers that respond successfully are included in the result.
    /// </summary>
    public async Task<IReadOnlyList<ShippingQuote>> GetAllQuotesAsync(
        ShippingRequest request, CancellationToken ct = default)
    {
        var tasks = new List<Task<ShippingQuote?>>
        {
            Task.Run(() => GetFedExQuoteAsync(request, ct), ct),
            Task.Run(() => GetDhlQuoteAsync(request, ct), ct),
        };

        var results = await Task.WhenAll(tasks);
        return results.Where(q => q is not null).Select(q => q!).ToList();
    }

    /// <summary>
    /// FedEx International Priority mock:
    /// rate = weight * 12.5 + declaredValue * 0.02 + 15 (base fee)
    /// Estimated transit: 2–4 business days
    /// </summary>
    private async Task<ShippingQuote?> GetFedExQuoteAsync(ShippingRequest request, CancellationToken ct)
    {
        ShippingQuote? quote = null;
        await _resiliencePipeline.ExecuteAsync(async _ =>
        {
            // TODO (v2): Replace with real FedEx Rate API call:
            // var response = await _httpClient.PostAsJsonAsync(
            //     "https://apis.fedex.com/rate/v1/...", BuildFedExRateRequest(request), ct);

            await Task.Delay(1, ct); // simulate network latency

            var baseRate = 15.0m;
            var weightCharge = request.WeightKg * 12.5m;
            var valueCharge = request.DeclaredValueUsd * 0.02m;
            var rate = Math.Round(baseRate + weightCharge + valueCharge, 2);

            quote = new ShippingQuote(
                Carrier: "FedEx",
                ServiceName: "International Priority",
                RateUsd: rate,
                EstimatedDays: Random.Shared.Next(2, 5),
                ExpiresAt: DateTime.UtcNow.AddHours(24)
            );
        }, ct);
        return quote;
    }

    /// <summary>
    /// DHL Express Worldwide mock:
    /// rate = weight * 10.5 + declaredValue * 0.015 + 12 (base fee)
    /// Estimated transit: 3–5 business days
    /// </summary>
    private async Task<ShippingQuote?> GetDhlQuoteAsync(ShippingRequest request, CancellationToken ct)
    {
        ShippingQuote? quote = null;
        await _resiliencePipeline.ExecuteAsync(async _ =>
        {
            // TODO (v2): Replace with real DHL API call:
            // var response = await _httpClient.PostAsJsonAsync(
            //     "https://api.dhl.com/...", BuildDhlQuoteRequest(request), ct);

            await Task.Delay(1, ct);

            var baseRate = 12.0m;
            var weightCharge = request.WeightKg * 10.5m;
            var valueCharge = request.DeclaredValueUsd * 0.015m;
            var rate = Math.Round(baseRate + weightCharge + valueCharge, 2);

            quote = new ShippingQuote(
                Carrier: "DHL",
                ServiceName: "Express Worldwide",
                RateUsd: rate,
                EstimatedDays: Random.Shared.Next(3, 6),
                ExpiresAt: DateTime.UtcNow.AddHours(24)
            );
        }, ct);
        return quote;
    }
}

public interface IShippingService
{
    Task<ShippingQuote?> GetQuoteAsync(ShippingRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<ShippingQuote>> GetAllQuotesAsync(ShippingRequest request, CancellationToken ct = default);
}

public record ShippingRequest(
    decimal WeightKg,
    string OriginCountryCode,
    string DestinationCountryCode,
    decimal DeclaredValueUsd
);

public record ShippingQuote(
    string Carrier,
    string ServiceName,
    decimal RateUsd,
    int EstimatedDays,
    DateTime ExpiresAt
);
