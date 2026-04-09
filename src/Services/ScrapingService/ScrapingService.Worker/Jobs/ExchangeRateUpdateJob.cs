using Microsoft.Extensions.Logging;
using Quartz;
using ScrapingService.Infrastructure.Services;

namespace ScrapingService.Worker.Jobs;

/// <summary>
/// Fetches the latest USD to VND exchange rate from an external API and caches it.
/// Runs every hour via Quartz.NET.
/// </summary>
[DisallowConcurrentExecution]
public class ExchangeRateUpdateJob : IJob
{
    private readonly ExchangeRateUpdateService _exchangeRateService;
    private readonly ILogger<ExchangeRateUpdateJob> _logger;

    public ExchangeRateUpdateJob(
        ExchangeRateUpdateService exchangeRateService,
        ILogger<ExchangeRateUpdateJob> logger)
    {
        _exchangeRateService = exchangeRateService;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogInformation("[{Time}] ExchangeRateUpdateJob started", DateTime.UtcNow);

        try
        {
            var rate = await _exchangeRateService.UpdateAndGetRateAsync(context.CancellationToken);
            _logger.LogInformation(
                "[{Time}] ExchangeRateUpdateJob completed: 1 USD = {Rate} VND",
                DateTime.UtcNow, rate);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{Time}] ExchangeRateUpdateJob failed", DateTime.UtcNow);
            throw new JobExecutionException(ex);
        }
    }
}
