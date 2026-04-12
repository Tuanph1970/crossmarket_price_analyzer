using Common.Application.Interfaces;
using Common.Domain.Scraping;
using Common.Infrastructure.Configuration;
using Common.Infrastructure.Proxy;
using Common.Infrastructure.Resilience;
using Polly;
using Polly.Extensions.Http;
using Quartz;
using ScrapingService.Infrastructure.Scrapers;
using ScrapingService.Infrastructure.Services;
using ScrapingService.Worker;
using ScrapingService.Worker.Jobs;

var builder = Host.CreateApplicationBuilder(args);

// 1. Infrastructure (logging, Redis, RabbitMQ, OpenTelemetry)
builder.Services.AddCommonInfrastructure(builder.Configuration, "ScrapingService");

// 2. HTTP clients — all with Polly resilience (retry + circuit breaker)
builder.Services.AddHttpClient("ProductService")
    .ConfigureHttpClient(c => c.BaseAddress = new Uri("http://product-api:8080/"))
    .AddStandardResilienceHandler(opts =>
    {
        opts.Retry.MaxRetryAttempts = 3;
        opts.Retry.BackoffType = DelayBackoffType.Exponential;
        opts.Timeout.Timeout = TimeSpan.FromSeconds(30);
    });

builder.Services.AddHttpClient("ScoringService")
    .ConfigureHttpClient(c => c.BaseAddress = new Uri("http://scoring-api:8080/"))
    .AddStandardResilienceHandler(opts =>
    {
        opts.Retry.MaxRetryAttempts = 3;
        opts.Retry.BackoffType = DelayBackoffType.Exponential;
        opts.Timeout.Timeout = TimeSpan.FromSeconds(30);
    });

builder.Services.AddHttpClient("ExchangeRate")
    .ConfigureHttpClient(c => c.Timeout = TimeSpan.FromSeconds(30))
    .AddStandardResilienceHandler(opts =>
    {
        opts.Retry.MaxRetryAttempts = 3;
        opts.Retry.BackoffType = DelayBackoffType.Exponential;
        opts.CircuitBreaker.FailureRatio = 0.5;
        opts.CircuitBreaker.MinimumThroughput = 5;
        opts.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(30);
        opts.Timeout.Timeout = TimeSpan.FromSeconds(30);
    });

builder.Services.AddHttpClient("Shopee")
    .ConfigureHttpClient(c => c.Timeout = TimeSpan.FromSeconds(30))
    .AddStandardResilienceHandler(opts =>
    {
        opts.Retry.MaxRetryAttempts = 2;
        opts.Retry.BackoffType = DelayBackoffType.Exponential;
        opts.CircuitBreaker.FailureRatio = 0.5;
        opts.CircuitBreaker.MinimumThroughput = 5;
        opts.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(30);
        opts.Timeout.Timeout = TimeSpan.FromSeconds(30);
    });

// 3. Register scrapers (all IProductScraper implementations)
builder.Services.AddSingleton<IProductScraper, AmazonScraper>();
builder.Services.AddSingleton<IProductScraper, WalmartScraper>();
builder.Services.AddSingleton<IProductScraper, CigarPageScraper>();

// 4. Register services
builder.Services.AddSingleton<ExchangeRateUpdateService>();

// Rotating proxy service with health-check HttpClient
builder.Services.AddHttpClient("RotatingProxyHealthCheck")
    .ConfigureHttpClient(c => c.Timeout = TimeSpan.FromSeconds(10));
builder.Services.AddSingleton<IRotatingProxyService, RotatingProxyService>();

// ShopeeApiClient uses the named "Shopee" HttpClient which carries Polly resilience
builder.Services.AddHttpClient<ShopeeApiClient>("Shopee")
    .ConfigureHttpClient(c => c.Timeout = TimeSpan.FromSeconds(30))
    .AddStandardResilienceHandler(opts =>
    {
        opts.Retry.MaxRetryAttempts = 2;
        opts.Retry.BackoffType = DelayBackoffType.Exponential;
        opts.CircuitBreaker.FailureRatio = 0.5;
        opts.CircuitBreaker.MinimumThroughput = 5;
        opts.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(30);
        opts.Timeout.Timeout = TimeSpan.FromSeconds(30);
    });

builder.Services.AddHttpClient<LazadaApiClient>("Lazada")
    .ConfigureHttpClient(c => c.Timeout = TimeSpan.FromSeconds(30))
    .AddStandardResilienceHandler(opts =>
    {
        opts.Retry.MaxRetryAttempts = 2;
        opts.Retry.BackoffType = DelayBackoffType.Exponential;
        opts.CircuitBreaker.FailureRatio = 0.5;
        opts.CircuitBreaker.MinimumThroughput = 5;
        opts.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(30);
        opts.Timeout.Timeout = TimeSpan.FromSeconds(30);
    });

// P3-B01: Lazada API client (HTTP-based, like ShopeeApiClient)
// AddHttpClient<T> registers both the named HttpClient AND T as the concrete type
// so the class is available for injection directly (no separate AddSingleton needed)

// P3-B02: Tiki scraper (Playwright-based, like Amazon/Walmart scrapers)
builder.Services.AddSingleton<IProductScraper, TikiScraper>();

// 5. Quartz.NET job scheduler
builder.Services.AddQuartz(q =>
{
    q.UseMicrosoftDependencyInjectionJobFactory();
    q.AddQuartzJobs();
});

builder.Services.AddQuartzHostedService(q => q.WaitForJobsToComplete = true);

var host = builder.Build();
host.Run();
