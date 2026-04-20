using Common.Application.Extensions;
using Common.Application.Interfaces;
using Common.Domain.Scraping;
using Common.Infrastructure.Configuration;
using Common.Infrastructure.Proxy;
using Polly.Retry;
using Quartz;
using BackoffStrategy = Common.Application.Extensions.ServiceCollectionExtensions.BackoffStrategy;
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
    .AddCmaStandardResilienceHandler();

builder.Services.AddHttpClient("ScoringService")
    .ConfigureHttpClient(c => c.BaseAddress = new Uri("http://scoring-api:8080/"))
    .AddCmaStandardResilienceHandler();

builder.Services.AddHttpClient("ExchangeRate")
    .ConfigureHttpClient(c => c.Timeout = TimeSpan.FromSeconds(30))
    .AddCmaResilientHttpClient(opts =>
    {
        opts.MaxRetryAttempts = 3;
        opts.BackoffType = BackoffStrategy.Exponential;
        opts.CircuitBreakerFailureRatio = 0.5;
        opts.CircuitBreakerMinimumThroughput = 5;
        opts.CircuitBreakerBreakDuration = TimeSpan.FromSeconds(30);
    });

builder.Services.AddHttpClient("Shopee")
    .ConfigureHttpClient(c => c.Timeout = TimeSpan.FromSeconds(30))
    .AddCmaResilientHttpClient(opts =>
    {
        opts.MaxRetryAttempts = 2;
        opts.BackoffType = BackoffStrategy.Exponential;
        opts.CircuitBreakerFailureRatio = 0.5;
        opts.CircuitBreakerMinimumThroughput = 5;
        opts.CircuitBreakerBreakDuration = TimeSpan.FromSeconds(30);
    });

// 3. Register scrapers (all IProductScraper implementations)
builder.Services.AddSingleton<IProductScraper, AmazonScraper>();
builder.Services.AddSingleton<IProductScraper, WalmartScraper>();
builder.Services.AddSingleton<IProductScraper, CigarPageScraper>();

// 4. Register services
// Adapters: wire local IHttpClientFactory / IRedisCacheService to Common.Infrastructure implementations
builder.Services.AddScoped<IHttpClientFactoryWrapper, HttpClientFactoryWrapper>();
builder.Services.AddScoped<IRedisCacheService, RedisCacheServiceAdapter>();
builder.Services.AddSingleton<ExchangeRateUpdateService>();

// Rotating proxy service with health-check HttpClient
builder.Services.AddHttpClient("RotatingProxyHealthCheck")
    .ConfigureHttpClient(c => c.Timeout = TimeSpan.FromSeconds(10));
builder.Services.AddSingleton<IRotatingProxyService, RotatingProxyService>();

// ShopeeApiClient uses the named "Shopee" HttpClient which carries Polly resilience
builder.Services.AddHttpClient<ShopeeApiClient>("Shopee")
    .ConfigureHttpClient(c => c.Timeout = TimeSpan.FromSeconds(30))
    .AddCmaResilientHttpClient(opts =>
    {
        opts.MaxRetryAttempts = 2;
        opts.BackoffType = BackoffStrategy.Exponential;
        opts.CircuitBreakerFailureRatio = 0.5;
        opts.CircuitBreakerMinimumThroughput = 5;
        opts.CircuitBreakerBreakDuration = TimeSpan.FromSeconds(30);
    });

builder.Services.AddHttpClient<LazadaApiClient>("Lazada")
    .ConfigureHttpClient(c => c.Timeout = TimeSpan.FromSeconds(30))
    .AddCmaResilientHttpClient(opts =>
    {
        opts.MaxRetryAttempts = 2;
        opts.BackoffType = BackoffStrategy.Exponential;
        opts.CircuitBreakerFailureRatio = 0.5;
        opts.CircuitBreakerMinimumThroughput = 5;
        opts.CircuitBreakerBreakDuration = TimeSpan.FromSeconds(30);
    });

// P3-B01: Lazada API client (HTTP-based, like ShopeeApiClient)
// P3-B02: Tiki scraper (Playwright-based, like Amazon/Walmart scrapers)
builder.Services.AddSingleton<IProductScraper, TikiScraper>();

// 5. Quartz.NET job scheduler
builder.Services.AddQuartz(q =>
{
    q.AddQuartzJobs();
});

builder.Services.AddQuartzHostedService(q => q.WaitForJobsToComplete = true);

var host = builder.Build();
host.Run();
