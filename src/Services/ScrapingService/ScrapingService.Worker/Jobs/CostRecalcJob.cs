using Microsoft.Extensions.Logging;
using Quartz;

namespace ScrapingService.Worker.Jobs;

/// <summary>
/// Recalculates landed costs for all active product matches.
/// Runs every 6 hours (at minute 15) via Quartz.NET.
/// Calls the ScoringService to perform the recalculation.
/// </summary>
[DisallowConcurrentExecution]
public class CostRecalcJob : IJob
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<CostRecalcJob> _logger;

    public CostRecalcJob(
        IHttpClientFactory httpClientFactory,
        ILogger<CostRecalcJob> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var startedAt = DateTime.UtcNow;
        _logger.LogInformation("[{Time}] CostRecalcJob started", startedAt);

        try
        {
            var client = _httpClientFactory.CreateClient("ScoringService");
            var response = await client.PostAsync(
                "/api/scores/recalculate",
                null,
                context.CancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation(
                    "[{Time}] CostRecalcJob completed successfully in {Duration}s",
                    DateTime.UtcNow, (DateTime.UtcNow - startedAt).TotalSeconds);
            }
            else
            {
                var body = await response.Content.ReadAsStringAsync(context.CancellationToken);
                _logger.LogWarning(
                    "[{Time}] CostRecalcJob returned {Status}: {Body}",
                    DateTime.UtcNow, response.StatusCode, body);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{Time}] CostRecalcJob failed", DateTime.UtcNow);
            throw new JobExecutionException(ex);
        }
    }
}
