using Quartz;
using ScrapingService.Worker.Jobs;

namespace ScrapingService.Worker;

public static class JobsConfiguration
{
    public static IServiceCollectionQuartzConfigurator AddQuartzJobs(
        this IServiceCollectionQuartzConfigurator quartz)
    {
        // ExchangeRateUpdateJob — runs every hour at minute 0
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

        // CostRecalcJob — runs every 6 hours (offset by 15 minutes from ScoringJob)
        quartz.AddJob<CostRecalcJob>(j => j.WithIdentity("CostRecalcJob"));
        quartz.AddTrigger(t => t
            .ForJob("CostRecalcJob")
            .WithIdentity("CostRecalcJob-trigger")
            .WithCronSchedule("0 15 */6 * * ?"));

        return quartz;
    }
}
