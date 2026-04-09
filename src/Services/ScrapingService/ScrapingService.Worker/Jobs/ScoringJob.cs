using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Quartz;

namespace ScrapingService.Worker.Jobs;

/// <summary>
/// Re-applies multi-factor scoring to all confirmed product matches.
/// Runs every 6 hours via Quartz.NET.
/// Calls the ScoringService to recalculate scores.
/// </summary>
[DisallowConcurrentExecution]
public class ScoringJob : IJob
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ScoringJob> _logger;

    public ScoringJob(
        IHttpClientFactory httpClientFactory,
        ILogger<ScoringJob> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var startedAt = DateTime.UtcNow;
        _logger.LogInformation("[{Time}] ScoringJob started", startedAt);

        try
        {
            var client = _httpClientFactory.CreateClient("ScoringService");

            // Get all confirmed matches from MatchingService
            var matchesResponse = await client.GetAsync(
                "/api/matches?status=Confirmed",
                context.CancellationToken);

            if (!matchesResponse.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "[{Time}] ScoringJob: failed to get matches ({Status})",
                    DateTime.UtcNow, matchesResponse.StatusCode);
                return;
            }

            var matches = await matchesResponse.Content.ReadFromJsonAsync<List<MatchDto>>(
                cancellationToken: context.CancellationToken);

            if (matches == null || matches?.Count == 0)
            {
                _logger.LogInformation("[{Time}] ScoringJob: no confirmed matches to score", DateTime.UtcNow);
                return;
            }

            var successCount = 0;
            var failCount = 0;

            foreach (var match in matches)
            {
                if (context.CancellationToken.IsCancellationRequested) break;

                try
                {
                    var scoreResponse = await client.PostAsync(
                        $"/api/scores?matchId={match.Id}",
                        null,
                        context.CancellationToken);

                    if (scoreResponse.IsSuccessStatusCode)
                        successCount++;
                    else
                        failCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to score match {MatchId}", match.Id);
                    failCount++;
                }
            }

            _logger.LogInformation(
                "[{Time}] ScoringJob completed: {Success} scored, {Failed} failed in {Duration}s",
                DateTime.UtcNow, successCount, failCount, (DateTime.UtcNow - startedAt).TotalSeconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{Time}] ScoringJob failed", DateTime.UtcNow);
            throw new JobExecutionException(ex);
        }
    }

    private record MatchDto(Guid Id);
}
