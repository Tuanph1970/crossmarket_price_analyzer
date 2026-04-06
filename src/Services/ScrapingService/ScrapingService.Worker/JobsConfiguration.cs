using Quartz;

namespace ScrapingService.Worker;

public static class JobsConfiguration
{
    public static IServiceCollectionQuartzConfigurator AddQuartzJobs(
        this IServiceCollectionQuartzConfigurator quartz)
    {
        // ExchangeRateUpdateJob — runs hourly
        quartz.AddJob<ExchangeRateUpdateJob>(j => j.WithIdentity("ExchangeRateUpdateJob"));
        quartz.AddTrigger(t => t
            .ForJob("ExchangeRateUpdateJob")
            .WithIdentity("ExchangeRateUpdateJob-trigger")
            .WithCronSchedule("0 0 * * * ?"));

        // UsProductScrapingJob — runs daily at 2am
        quartz.AddJob<UsProductScrapingJob>(j => j.WithIdentity("UsProductScrapingJob"));
        quartz.AddTrigger(t => t
            .ForJob("UsProductScrapingJob")
            .WithIdentity("UsProductScrapingJob-trigger")
            .WithCronSchedule("0 0 2 * * ?"));

        // VnProductScrapingJob — runs daily at 3am
        quartz.AddJob<VnProductScrapingJob>(j => j.WithIdentity("VnProductScrapingJob"));
        quartz.AddTrigger(t => t
            .ForJob("VnProductScrapingJob")
            .WithIdentity("VnProductScrapingJob-trigger")
            .WithCronSchedule("0 0 3 * * ?"));

        // ScoringJob — runs every 6 hours
        quartz.AddJob<ScoringJob>(j => j.WithIdentity("ScoringJob"));
        quartz.AddTrigger(t => t
            .ForJob("ScoringJob")
            .WithIdentity("ScoringJob-trigger")
            .WithCronSchedule("0 0 */6 * * ?"));

        return quartz;
    }
}

// Stub job implementations — Phase 1 will implement actual scraping
public class ExchangeRateUpdateJob : IJob
{
    public async Task Execute(IJobExecutionContext context) =>
        await Console.Out.WriteLineAsync($"[{DateTime.UtcNow}] ExchangeRateUpdateJob ran.");
}

public class UsProductScrapingJob : IJob
{
    public async Task Execute(IJobExecutionContext context) =>
        await Console.Out.WriteLineAsync($"[{DateTime.UtcNow}] UsProductScrapingJob ran.");
}

public class VnProductScrapingJob : IJob
{
    public async Task Execute(IJobExecutionContext context) =>
        await Console.Out.WriteLineAsync($"[{DateTime.UtcNow}] VnProductScrapingJob ran.");
}

public class ScoringJob : IJob
{
    public async Task Execute(IJobExecutionContext context) =>
        await Console.Out.WriteLineAsync($"[{DateTime.UtcNow}] ScoringJob ran.");
}
