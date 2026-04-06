using Common.Application.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Common.Application.Behaviors;

/// <summary>
/// MediatR pipeline behavior that caches query responses in Redis.
/// </summary>
public class CachingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
    where TResponse : class
{
    private readonly ICacheService _cache;
    private readonly ILogger<CachingBehavior<TRequest, TResponse>> _logger;

    public CachingBehavior(ICacheService cache, ILogger<CachingBehavior<TRequest, TResponse>> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        // Only cache requests that have the ICacheableRequest marker interface
        if (request is not ICacheableRequest cacheable)
            return await next();

        var cacheKey = cacheable.CacheKey;
        var cacheTtl = cacheable.CacheTtl;

        var cached = await _cache.GetAsync<TResponse>(cacheKey, cancellationToken);
        if (cached != null)
        {
            _logger.LogDebug("Cache hit for key: {CacheKey}", cacheKey);
            return cached;
        }

        _logger.LogDebug("Cache miss for key: {CacheKey}", cacheKey);
        var response = await next();

        await _cache.SetAsync(cacheKey, response, cacheTtl, cancellationToken);
        return response;
    }
}

/// <summary>
/// Marker interface to indicate a request whose response should be cached.
/// </summary>
public interface ICacheableRequest
{
    string CacheKey { get; }
    TimeSpan CacheTtl { get; }
}
