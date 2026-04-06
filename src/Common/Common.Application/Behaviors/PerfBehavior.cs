using MediatR;
using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Common.Application.Behaviors;

/// <summary>
/// MediatR pipeline behavior that tracks performance metrics per request.
/// </summary>
public class PerfBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ILogger<PerfBehavior<TRequest, TResponse>> _logger;

    public PerfBehavior(ILogger<PerfBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        var requestName = typeof(TRequest).Name;

        try
        {
            return await next();
        }
        finally
        {
            sw.Stop();
            var elapsedMs = sw.ElapsedMilliseconds;

            if (elapsedMs > 1000)
            {
                _logger.LogWarning(
                    "Slow request detected: {RequestName} took {ElapsedMs}ms (threshold: 1000ms)",
                    requestName, elapsedMs);
            }
            else
            {
                _logger.LogDebug(
                    "{RequestName} completed in {ElapsedMs}ms",
                    requestName, elapsedMs);
            }
        }
    }
}
