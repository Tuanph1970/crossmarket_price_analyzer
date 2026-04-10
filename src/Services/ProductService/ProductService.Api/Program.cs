using Common.Application.Extensions;
using Common.Application.Interfaces;
using Common.Domain.Scraping;
using Common.Infrastructure.Configuration;
using Microsoft.EntityFrameworkCore;
using ProductService.Application.Commands;
using ProductService.Application.DTOs;
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

// 5. HTTP clients
builder.Services.AddHttpClient("ExchangeRate")
    .ConfigureHttpClient(c => c.Timeout = TimeSpan.FromSeconds(30));

// 6. Register scrapers (IProductScraper implementations)
builder.Services.AddSingleton<IProductScraper, AmazonScraper>();
builder.Services.AddSingleton<IProductScraper, WalmartScraper>();
builder.Services.AddSingleton<IProductScraper, CigarPageScraper>();

// 7. Register infrastructure services
builder.Services.AddScoped<ScraperFactory>();
builder.Services.AddScoped<IExchangeRateService, ExchangeRateService>();

// 8. Register application services
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
});

// GET /api/products/{id}
app.MapGet("/api/products/{id:guid}", async (Guid id, IProductService svc, CancellationToken ct) =>
{
    var result = await svc.GetByIdAsync(id, ct);
    return result is null ? Results.NotFound() : Results.Ok(result);
});

// POST /api/products
app.MapPost("/api/products", async (
    CreateProductRequest req,
    IProductService svc,
    CancellationToken ct) =>
{
    var result = await svc.CreateAsync(req.Name, req.SourceUrl, req.Source,
        req.Sku, req.HsCode, req.BrandId, req.CategoryId, req.IsActive, ct);
    return Results.Created($"/api/products/{result.Id}", result);
});

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
});

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
});

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
});

// POST /api/products/quick-lookup — URL → scrape → match → score
app.MapPost("/api/products/quick-lookup", async (
    MediatR.IMediator mediator,
    QuickLookupRequest req,
    CancellationToken ct) =>
{
    var result = await mediator.Send(
        new QuickLookupCommand(req.Url, req.VnNameFilter, req.MaxVnMatches, req.MinMatchScore),
        ct);
    return Results.Ok(result);
});

// Health check
app.MapGet("/health", () => Results.Ok(new { Status = "Healthy", Service = "ProductService", Timestamp = DateTime.UtcNow }));

app.Run();

// Request DTOs
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

public record UpdateProductRequest(
    string? Name = null,
    string? Sku = null,
    string? HsCode = null,
    Guid? BrandId = null,
    Guid? CategoryId = null,
    bool? IsActive = null
);

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

// Request DTO for POST /api/products/quick-lookup
public record QuickLookupRequest(
    string Url,
    string? VnNameFilter = null,
    int MaxVnMatches = 5,
    decimal MinMatchScore = 40m
);
