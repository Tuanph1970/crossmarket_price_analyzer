using Common.Application.Extensions;
using Common.Application.Interfaces;
using Common.Domain.Scraping;
using Common.Infrastructure.Configuration;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.EntityFrameworkCore;
using ProductService.Application.Commands;
using ProductService.Application.DTOs;
using ProductService.Application.Persistence;
using ProductService.Application.Services;
using ProductService.Contracts.Persistence;
using ProductService.Infrastructure.Persistence;
using ProductService.Infrastructure.Services;
using ProductService.Infrastructure.Services.ProductScrapers;

var builder = WebApplication.CreateBuilder(args);

// 1. Add services
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "ProductService API", Version = "v1" });
    var xmlFile = $"{typeof(Program).Assembly.GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
        c.IncludeXmlComments(xmlPath);
});

// 2. Add infrastructure (DB, Redis, RabbitMQ, OpenTelemetry, Serilog)
builder.Host.UseCommonLogging();
builder.Services.AddCommonInfrastructure(builder.Configuration, "ProductService");

// 3. Add application layer (MediatR, validators, pipeline behaviors)
builder.Services.AddCommonApplication();

// 4. Register DbContext
builder.Services.AddDbContext<ProductDbContext>(options =>
{
    var cs = builder.Configuration.GetConnectionString("DefaultConnection");
    options.UseMySql(cs, ServerVersion.AutoDetect(cs));
});

// 4b. Register IProductDbContext alias
builder.Services.AddScoped<IProductDbContext>(sp => sp.GetRequiredService<ProductDbContext>());

// 5. HTTP clients — with Polly resilience
builder.Services.AddHttpClient("ExchangeRate")
    .ConfigureHttpClient(c => c.Timeout = TimeSpan.FromSeconds(30))
    .AddCmaStandardResilienceHandler();

// 5b. HTTP clients for cross-service persistence (after Quick Lookup)
builder.Services.AddHttpClient("MatchingService")
    .ConfigureHttpClient(c => c.BaseAddress = new Uri(
        builder.Configuration["Services:MatchingServiceUrl"] ?? "http://matching-api:8080/"));
builder.Services.AddHttpClient("ScoringService")
    .ConfigureHttpClient(c => c.BaseAddress = new Uri(
        builder.Configuration["Services:ScoringServiceUrl"] ?? "http://scoring-api:8080/"));

// 6. Register scrapers (IProductScraper implementations)
builder.Services.AddSingleton<IProductScraper, AmazonScraper>();
builder.Services.AddSingleton<IProductScraper, WalmartScraper>();
builder.Services.AddSingleton<IProductScraper, CigarPageScraper>();

// 7. Register infrastructure services
builder.Services.AddScoped<ScraperFactory>();
builder.Services.AddScoped<IExchangeRateService, ExchangeRateService>();

// 7b. Fallback stubs for cross-service dependencies (QuickLookupCommandHandler uses interfaces)
builder.Services.AddScoped<IFuzzyMatchingService, Common.Infrastructure.Services.FallbackFuzzyMatchingService>();
builder.Services.AddScoped<ILandedCostCalculator, Common.Infrastructure.Services.FallbackLandedCostCalculator>();
builder.Services.AddScoped<IScoringEngine, Common.Infrastructure.Services.FallbackScoringEngine>();

// 8. Register application services (including repository used by MediatR handlers)
builder.Services.AddScoped<ProductRepository>();
builder.Services.AddScoped<IProductService, ProductServiceImpl>();

// 9. MediatR — register QuickLookupCommand handler
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssemblyContaining<ProductServiceImpl>());

var app = builder.Build();

// 6. Apply migrations on startup (development only)
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ProductDbContext>();
    await db.Database.EnsureCreatedAsync();
}

// 7. Configure pipeline
app.UseSwagger();
app.UseSwaggerUI();

// GET /api/products — list with pagination
app.MapGet("/api/products", async (
    IProductService svc,
    int page = 1,
    int pageSize = 20,
    string? source = null,
    Guid? categoryId = null,
    bool? isActive = null,
    CancellationToken ct = default) =>
{
    Common.Domain.Enums.ProductSource? src = null;
    if (!string.IsNullOrEmpty(source) && Enum.TryParse<Common.Domain.Enums.ProductSource>(source, true, out var s))
        src = s;
    var result = await svc.GetProductsAsync(page, pageSize, src, categoryId, isActive, ct);
    return Results.Ok(result);
})
.Produces<PaginatedProductsDto>(StatusCodes.Status200OK)
.WithTags("Products")
.WithName("GetProducts")
.WithDescription("Returns a paginated list of products with optional filtering by source, category, and active status.");

// GET /api/products/{id}
app.MapGet("/api/products/{id:guid}", async (
    Guid id,
    IProductService svc,
    CancellationToken ct) =>
{
    var result = await svc.GetByIdAsync(id, ct);
    return result is null ? Results.NotFound() : Results.Ok(result);
})
.Produces<ProductDto>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status404NotFound)
.WithTags("Products")
.WithName("GetProductById")
.WithDescription("Returns a single product by its unique identifier.");

// POST /api/products
app.MapPost("/api/products", async (
    CreateProductRequest req,
    IProductService svc,
    CancellationToken ct) =>
{
    var result = await svc.CreateAsync(req.Name, req.SourceUrl, req.Source,
        req.Sku, req.HsCode, req.BrandId, req.CategoryId, req.IsActive, ct);
    return Results.Created($"/api/products/{result.Id}", result);
})
.Produces<ProductDto>(StatusCodes.Status201Created)
.WithTags("Products")
.WithName("CreateProduct")
.WithDescription("Creates a new product record.");

// PUT /api/products/{id}
app.MapPut("/api/products/{id:guid}", async (
    Guid id,
    UpdateProductRequest req,
    IProductService svc,
    CancellationToken ct) =>
{
    var result = await svc.UpdateAsync(id, req.Name, req.Sku, req.HsCode,
        req.BrandId, req.CategoryId, req.IsActive, ct);
    return result is null ? Results.NotFound() : Results.Ok(result);
})
.Produces<ProductDto>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status404NotFound)
.WithTags("Products")
.WithName("UpdateProduct")
.WithDescription("Updates an existing product by its unique identifier.");

// GET /api/products/{id}/price-history
app.MapGet("/api/products/{id:guid}/price-history", async (
    Guid id,
    DateTime? from,
    DateTime? to,
    int limit = 30,
    IProductService? svc = null,
    CancellationToken ct = default) =>
{
    var result = await svc!.GetPriceHistoryAsync(id, from, to, limit, ct);
    return Results.Ok(result);
})
.Produces<List<PriceSnapshotDto>>(StatusCodes.Status200OK)
.WithTags("Products")
.WithName("GetProductPriceHistory")
.WithDescription("Returns the price history for a specific product within an optional date range.");

// POST /api/products/upsert-from-scrape
app.MapPost("/api/products/upsert-from-scrape", async (
    UpsertProductRequest req,
    IProductService svc,
    CancellationToken ct) =>
{
    var result = await svc.UpsertFromScrapeAsync(req.Name, req.Brand, req.Sku,
        req.Price, req.Currency, req.QuantityPerUnit, req.SellerName,
        req.SellerRating, req.SalesVolume, req.SourceUrl, req.Source,
        req.HsCode, req.CategoryName, ct);
    return Results.Ok(result);
})
.Produces<ProductDto>(StatusCodes.Status200OK)
.WithTags("Scraping")
.WithName("UpsertProductFromScrape")
.WithDescription("Upserts a product from scraped data (used by the scraping worker).");

// POST /api/products/quick-lookup — URL → scrape → match → score
app.MapPost("/api/products/quick-lookup", async (
    MediatR.IMediator mediator,
    QuickLookupRequest req,
    CancellationToken ct) =>
{
    try
    {
        var result = await mediator.Send(
            new QuickLookupCommand(req.Url, req.VnNameFilter, req.MaxVnMatches, req.MinMatchScore),
            ct);
        return Results.Ok(result);
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { message = ex.Message });
    }
})
.Produces<QuickLookupResultDto>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status400BadRequest)
.WithTags("Products")
.WithName("QuickLookup")
.WithDescription("Scrapes a source URL and returns matching Vietnam products with scores.");

// POST /api/products/scrape-listing — listing page URL → extract product URLs → scrape each
app.MapPost("/api/products/scrape-listing", async (
    MediatR.IMediator mediator,
    ScrapeListingRequest req,
    CancellationToken ct) =>
{
    try
    {
        var result = await mediator.Send(
            new ScrapeListingCommand(req.PageUrl, req.MaxProducts),
            ct);
        return Results.Ok(result);
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { message = ex.Message });
    }
})
.Produces<ScrapeListingResultDto>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status400BadRequest)
.WithTags("Products")
.WithName("ScrapeListing")
.WithDescription("Scrapes a category/listing page and returns all individual products found on it.");

// Health check
app.MapGet("/health", () => Results.Ok(new { Status = "Healthy", Service = "ProductService", Timestamp = DateTime.UtcNow }))
   .WithTags("Health")
   .WithName("HealthCheck")
   .WithDescription("Returns the health status of the ProductService.");

app.Run();

/// <summary>
/// Request to create a new product.
/// </summary>
/// <param name="Name">Product display name.</param>
/// <param name="SourceUrl">Original source URL where the product was found.</param>
/// <param name="Source">Retail source (Amazon, Walmart, etc.).</param>
/// <param name="Sku">Optional SKU or part number.</param>
/// <param name="HsCode">Optional Harmonized System code for customs.</param>
/// <param name="BrandId">Optional brand identifier.</param>
/// <param name="CategoryId">Optional category identifier.</param>
/// <param name="IsActive">Whether the product is active for matching (default: true).</param>
public record CreateProductRequest(
    string Name,
    string SourceUrl,
    Common.Domain.Enums.ProductSource Source,
    string? Sku = null,
    string? HsCode = null,
    Guid? BrandId = null,
    Guid? CategoryId = null,
    bool IsActive = true
);

/// <summary>
/// Request to update an existing product.
/// </summary>
/// <param name="Name">Updated product name (optional).</param>
/// <param name="Sku">Updated SKU (optional).</param>
/// <param name="HsCode">Updated HS code (optional).</param>
/// <param name="BrandId">Updated brand ID (optional).</param>
/// <param name="CategoryId">Updated category ID (optional).</param>
/// <param name="IsActive">Updated active flag (optional).</param>
public record UpdateProductRequest(
    string? Name = null,
    string? Sku = null,
    string? HsCode = null,
    Guid? BrandId = null,
    Guid? CategoryId = null,
    bool? IsActive = null
);

/// <summary>
/// Request to upsert a product from scraped data.
/// </summary>
/// <param name="Name">Scraped product name.</param>
/// <param name="Brand">Brand name from scraped data.</param>
/// <param name="Sku">SKU from scraped data.</param>
/// <param name="Price">Scraped price amount.</param>
/// <param name="Currency">Price currency code (e.g., USD).</param>
/// <param name="QuantityPerUnit">Quantity per sellable unit.</param>
/// <param name="SellerName">Name of the seller on the platform.</param>
/// <param name="SellerRating">Seller rating out of 5 (optional).</param>
/// <param name="SalesVolume">Number of sales in the observed period (optional).</param>
/// <param name="SourceUrl">Source URL of the scraped product.</param>
/// <param name="Source">Source platform enumeration.</param>
/// <param name="HsCode">Optional HS code.</param>
/// <param name="CategoryName">Category name from the source platform.</param>
public record UpsertProductRequest(
    string Name,
    string? Brand,
    string? Sku,
    decimal Price,
    string Currency,
    decimal QuantityPerUnit,
    string? SellerName,
    decimal? SellerRating,
    int? SalesVolume,
    string SourceUrl,
    Common.Domain.Enums.ProductSource Source,
    string? HsCode = null,
    string? CategoryName = null
);

/// <summary>
/// Request to perform a quick lookup: scrape a source URL and find matching Vietnam products.
/// </summary>
/// <param name="Url">Source product URL to scrape.</param>
/// <param name="VnNameFilter">Optional text filter for Vietnam product names.</param>
/// <param name="MaxVnMatches">Maximum number of Vietnam matches to return (default: 5).</param>
/// <param name="MinMatchScore">Minimum match score 0–100 (default: 40).</param>
public record QuickLookupRequest(
    string Url,
    string? VnNameFilter = null,
    int MaxVnMatches = 5,
    decimal MinMatchScore = 40m
);

/// <summary>
/// Request to scrape all products from a category/listing page.
/// </summary>
/// <param name="PageUrl">Category or listing page URL (e.g. cigarpage.com/samplers/...).</param>
/// <param name="MaxProducts">Maximum number of products to scrape (default: 15).</param>
public record ScrapeListingRequest(
    string PageUrl,
    int MaxProducts = 15
);
