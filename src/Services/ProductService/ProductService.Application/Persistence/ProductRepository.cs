using Common.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using ProductService.Contracts.Persistence;
using ProductService.Domain.Entities;

namespace ProductService.Application.Persistence;

public class ProductRepository
{
    private readonly IProductDbContext _context;

    public ProductRepository(IProductDbContext context)
    {
        _context = context;
    }

    public async Task<Product?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.Products
            .Include(p => p.Brand)
            .Include(p => p.Category)
            .Include(p => p.PriceSnapshots.OrderByDescending(s => s.ScrapedAt))
            .FirstOrDefaultAsync(p => p.Id == id, ct);
    }

    public async Task<IReadOnlyList<Product>> GetAllAsync(CancellationToken ct = default)
    {
        return await _context.Products
            .Include(p => p.Brand)
            .Include(p => p.Category)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<Product> AddAsync(Product entity, CancellationToken ct = default)
    {
        await _context.Products.AddAsync(entity, ct);
        await _context.SaveChangesAsync(ct);
        return entity;
    }

    public async Task UpdateAsync(Product entity, CancellationToken ct = default)
    {
        _context.Products.Update(entity);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.Products.AnyAsync(p => p.Id == id, ct);
    }

    public async Task<int> CountAsync(CancellationToken ct = default)
    {
        return await _context.Products.CountAsync(ct);
    }

    public async Task<(IReadOnlyList<Product> Items, int TotalCount)> GetPaginatedAsync(
        int page,
        int pageSize,
        ProductSource? source = null,
        Guid? categoryId = null,
        bool? isActive = null,
        CancellationToken ct = default)
    {
        var query = _context.Products.AsQueryable();

        if (source.HasValue)
            query = query.Where(p => p.Source == source.Value);
        if (categoryId.HasValue)
            query = query.Where(p => p.CategoryId == categoryId.Value);
        if (isActive.HasValue)
            query = query.Where(p => p.IsActive == isActive.Value);

        var total = await query.CountAsync(ct);
        var items = await query
            .Include(p => p.Brand)
            .Include(p => p.Category)
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    public async Task<IReadOnlyList<PriceSnapshot>> GetPriceHistoryAsync(
        Guid productId,
        DateTime? from = null,
        DateTime? to = null,
        int limit = 30,
        CancellationToken ct = default)
    {
        var query = _context.PriceSnapshots.Where(s => s.ProductId == productId);
        if (from.HasValue)
            query = query.Where(s => s.ScrapedAt >= from.Value);
        if (to.HasValue)
            query = query.Where(s => s.ScrapedAt <= to.Value);

        return await query
            .OrderByDescending(s => s.ScrapedAt)
            .Take(limit)
            .ToListAsync(ct);
    }

    public async Task<ExchangeRate?> GetLatestRateAsync(string from, string to, CancellationToken ct = default)
    {
        return await _context.ExchangeRates
            .Where(r => r.FromCurrency == from && r.ToCurrency == to)
            .OrderByDescending(r => r.FetchedAt)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<Product> AddPriceSnapshotAsync(Guid productId, PriceSnapshot snapshot, CancellationToken ct = default)
    {
        var product = await _context.Products.FindAsync(new object[] { productId }, ct)
            ?? throw new InvalidOperationException($"Product {productId} not found");
        product.PriceSnapshots.Add(snapshot);
        await _context.SaveChangesAsync(ct);
        return product;
    }

    public async Task<Brand?> GetOrCreateBrandAsync(string name, CancellationToken ct = default)
    {
        var normalized = name.ToLowerInvariant().Trim();
        var existing = await _context.Brands
            .FirstOrDefaultAsync(b => b.NormalizedName == normalized, ct);
        if (existing != null) return existing;

        var brand = Brand.Create(name);
        await _context.Brands.AddAsync(brand, ct);
        await _context.SaveChangesAsync(ct);
        return brand;
    }

    public async Task<Category?> GetOrCreateCategoryAsync(string name, string hsCode, CancellationToken ct = default)
    {
        var existing = await _context.Categories
            .FirstOrDefaultAsync(c => c.HsCode == hsCode, ct);
        if (existing != null) return existing;

        var category = Category.Create(name, hsCode);
        await _context.Categories.AddAsync(category, ct);
        await _context.SaveChangesAsync(ct);
        return category;
    }
}
