using Common.Application.Interfaces;
using Common.Domain.Enums;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using ProductService.Domain.Entities;
using ProductService.Infrastructure.Persistence;
using Xunit;

namespace ProductService.IntegrationTests;

using TestDbContext = ProductDbContext;

public class ProductTests : IClassFixture<ProductServiceTestFixture>
{
    private readonly ProductServiceTestFixture _fixture;

    public ProductTests(ProductServiceTestFixture fixture)
    {
        _fixture = fixture;
        fixture.ConfigureTestServices = services =>
        {
            // Override ICacheService with a no-op so the exchange-rate service
            // can be exercised without a real Redis connection during the fixture lifecycle.
            services.AddScoped<ICacheService, InMemoryCacheService>();
        };
    }

    [Fact]
    public async Task ProductRepository_Insert_ReturnsId()
    {
        // Arrange
        await using var scope = _fixture.WebAppFactory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TestDbContext>();

        var product = Product.Create(
            name: "Apple MacBook Pro 14\"",
            sourceUrl: "https://amazon.com/product/abc123",
            source: ProductSource.Amazon,
            sku: "MBP-14-M3",
            hsCode: "847130");

        // Act
        await db.Products.AddAsync(product);
        await db.SaveChangesAsync();

        // Assert
        product.Id.Should().NotBeEmpty();
        (await db.Products.FindAsync(product.Id)).Should().NotBeNull();
    }

    [Fact]
    public async Task ProductRepository_GetById_IncludesPriceSnapshots()
    {
        // Arrange
        await using var scope = _fixture.WebAppFactory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TestDbContext>();

        var product = Product.Create(
            name: "Sony WH-1000XM5",
            sourceUrl: "https://walmart.com/product/xyz",
            source: ProductSource.Walmart,
            sku: "WH1000XM5");

        var snapshots = Enumerable.Range(1, 3).Select(i => PriceSnapshot.Create(
            productId: product.Id,
            price: 100m * i,
            currency: "USD",
            quantityPerUnit: 1,
            sellerName: $"Seller{i}",
            sellerRating: 4.5m,
            salesVolume: 100 * i)).ToList();

        foreach (var s in snapshots)
            product.PriceSnapshots.Add(s);

        await db.Products.AddAsync(product);
        await db.SaveChangesAsync();

        // Detach to simulate a fresh query
        db.ChangeTracker.Clear();

        // Act
        var loaded = await db.Products
            .Include(p => p.PriceSnapshots)
            .FirstOrDefaultAsync(p => p.Id == product.Id);

        // Assert
        loaded.Should().NotBeNull();
        loaded!.PriceSnapshots.Should().HaveCount(3);
        loaded.PriceSnapshots.Select(s => s.Price).Should().ContainInOrder(100m, 200m, 300m);
    }

    [Fact]
    public async Task ExchangeRateService_FetchAndCache()
    {
        // Arrange
        await using var scope = _fixture.WebAppFactory.Services.CreateAsyncScope();
        var rateService = scope.ServiceProvider.GetRequiredService<IExchangeRateService>();
        var cache = scope.ServiceProvider.GetRequiredService<ICacheService>();

        // Act
        var rate = await rateService.UpdateAndGetRateAsync();

        // Assert
        rate.Should().BeGreaterThan(0m, "exchange rate must be a positive value");

        // Verify it was cached by calling GetCachedRateAsync (no new API call)
        var cachedRate = await rateService.GetCachedRateAsync();
        cachedRate.Should().Be(rate, "cached rate should match the fetched rate");
    }

    [Theory]
    [InlineData(30)]
    [InlineData(90)]
    public async Task PriceHistory_QueryByDateRange_ReturnsCorrectSnapshots(int days)
    {
        // Arrange
        await using var scope = _fixture.WebAppFactory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TestDbContext>();

        var product = Product.Create(
            name: "Test Product for Date Range",
            sourceUrl: "https://example.com/daterange",
            source: ProductSource.Shopee);

        var now = DateTime.UtcNow;
        var snapshots = new List<PriceSnapshot>();

        for (var i = 0; i < days; i++)
        {
            var snapshot = PriceSnapshot.Create(
                productId: product.Id,
                price: 50m + i,
                currency: "VND",
                quantityPerUnit: 1);
            snapshot.ScrapedAt = now.AddDays(-i);
            snapshots.Add(snapshot);
            product.PriceSnapshots.Add(snapshot);
        }

        await db.Products.AddAsync(product);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        // Act
        var from = now.AddDays(-(days - 1)).Date;
        var to = now.Date.AddDays(1); // inclusive upper bound

        var results = await db.PriceSnapshots
            .AsNoTracking()
            .Where(s => s.ProductId == product.Id && s.ScrapedAt >= from && s.ScrapedAt < to)
            .OrderByDescending(s => s.ScrapedAt)
            .ToListAsync();

        // Assert
        results.Should().HaveCount(days);
    }

    [Fact]
    public async Task Brand_Create_Then_Link_To_Product()
    {
        // Arrange
        await using var scope = _fixture.WebAppFactory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TestDbContext>();

        var brand = Brand.Create("Apple");
        await db.Brands.AddAsync(brand);
        await db.SaveChangesAsync();

        var product = Product.Create(
            name: "iPhone 15 Pro",
            sourceUrl: "https://amazon.com/iphone15",
            source: ProductSource.Amazon,
            brandId: brand.Id);

        await db.Products.AddAsync(product);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        // Act
        var loaded = await db.Products
            .Include(p => p.Brand)
            .FirstOrDefaultAsync(p => p.Id == product.Id);

        // Assert
        loaded.Should().NotBeNull();
        loaded!.Brand.Should().NotBeNull();
        loaded.Brand!.Name.Should().Be("Apple");
    }
}

// ─── Minimal in-memory cache service for testing ────────────────────────────
internal sealed class InMemoryCacheService : ICacheService
{
    private readonly Dictionary<string, object> _cache = new();
    private readonly Dictionary<string, DateTime> _expiries = new();

    public Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class
        => Task.FromResult(_cache.TryGetValue(key, out var v) ? v as T : null);

    public Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken ct = default) where T : class
    {
        _cache[key] = value!;
        if (expiry.HasValue)
            _expiries[key] = DateTime.UtcNow.Add(expiry.Value);
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken ct = default)
    {
        _cache.Remove(key);
        _expiries.Remove(key);
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(string key, CancellationToken ct = default)
        => Task.FromResult(_cache.ContainsKey(key));

    public Task<T?> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiry = null, CancellationToken ct = default) where T : class
    {
        if (_cache.TryGetValue(key, out var v))
            return Task.FromResult(v as T);

        return factory().ContinueWith(t =>
        {
            _cache[key] = t.Result!;
            return t.Result;
        }, ct);
    }
}

