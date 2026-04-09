using Common.Infrastructure.Configuration;
using Quartz;
using ScrapingService.Infrastructure.Scrapers;
using ScrapingService.Infrastructure.Services;
using ScrapingService.Worker;
using ScrapingService.Worker.Jobs;

var builder = Host.CreateApplicationBuilder(args);

// 1. Infrastructure (logging, Redis, RabbitMQ, OpenTelemetry)
builder.Services.AddCommonInfrastructure(builder.Configuration, "ScrapingService");

// 2. HTTP clients
builder.Services.AddHttpClient("ProductService")
    .ConfigureHttpClient(c => c.BaseAddress = new Uri("http://product-api:8080/"));
builder.Services.AddHttpClient("ScoringService")
    .ConfigureHttpClient(c => c.BaseAddress = new Uri("http://scoring-api:8080/"));
builder.Services.AddHttpClient("ExchangeRate")
    .ConfigureHttpClient(c => c.Timeout = TimeSpan.FromSeconds(30));

// 3. Register scrapers (all IProductScraper implementations)
builder.Services.AddSingleton<IProductScraper, AmazonScraper>();
builder.Services.AddSingleton<IProductScraper, WalmartScraper>();
builder.Services.AddSingleton<IProductScraper, CigarPageScraper>();

// 4. Register services
builder.Services.AddSingleton<ExchangeRateUpdateService>();
builder.Services.AddSingleton<ShopeeApiClient>(sp =>
{
    var httpClientFactory = sp.GetRequiredService<System.Net.Http.IHttpClientFactory>();
    var httpClient = httpClientFactory.CreateClient("ExchangeRate");
    var logger = sp.GetRequiredService<ILogger<ShopeeApiClient>>();
    var exchangeService = sp.GetRequiredService<IExchangeRateService>();
    return new ShopeeApiClient(httpClient, logger, exchangeService);
});

// 5. Quartz.NET job scheduler
builder.Services.AddQuartz(q =>
{
    q.UseMicrosoftDependencyInjectionJobFactory();
    q.AddQuartzJobs();
});

builder.Services.AddQuartzHostedService(q => q.WaitForJobsToComplete = true);

var host = builder.Build();
host.Run();
