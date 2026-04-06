using Common.Infrastructure.Configuration;
using Quartz;
using ScrapingService.Worker;

var builder = Host.CreateApplicationBuilder(args);

// Infrastructure
builder.Services.AddCommonInfrastructure(builder.Configuration, "ScrapingService");

// Quartz.NET for job scheduling
builder.Services.AddQuartz(q =>
{
    q.UseMicrosoftDependencyInjectionJobFactory();
    q.AddQuartzJobs();
});

builder.Services.AddQuartzHostedService(q => q.WaitForJobsToComplete = true);

var host = builder.Build();
host.Run();
