using MediatR;
using Microsoft.Extensions.Logging;

namespace Common.Application.Behaviors;

/// <summary>
/// MediatR pipeline behavior that logs all requests and responses with timing.
/// </summary>
public class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        var requestGuid = Guid.NewGuid().ToString();

        _logger.LogInformation(
            "[{RequestGuid}] {RequestName} handling {RequestType}",
            requestGuid, requestName, typeof(TRequest).FullName);

        try
        {
            var response = await next();
            _logger.LogInformation(
                "[{RequestGuid}] {RequestName} handled successfully",
                requestGuid, requestName);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "[{RequestGuid}] {RequestName} failed: {ErrorMessage}",
                requestGuid, requestName, ex.Message);
            throw;
        }
    }
}
