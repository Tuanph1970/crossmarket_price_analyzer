using Common.Domain.Enums;
using ProductService.Application.DTOs;
using ProductService.Application.Handlers;
using ProductService.Application.Persistence;
using ProductService.Domain.Entities;

namespace ProductService.Application.Services;

public class ProductServiceImpl : IProductService
{
    private readonly ProductRepository _repo;

    public ProductServiceImpl(ProductRepository repo) { _repo = repo; }

    public async Task<PaginatedProductsDto> GetProductsAsync(int page, int pageSize,
        ProductSource? source, Guid? categoryId, bool? isActive, CancellationToken ct)
    {
        var (items, total) = await _repo.GetPaginatedAsync(page, pageSize, source, categoryId, isActive, ct);
        return new PaginatedProductsDto(
            items.Select(ProductDtoMappers.ToListDto).ToList(),
            total, page, pageSize,
            (int)Math.Ceiling(total / (double)pageSize));
    }

    public async Task<ProductDto?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var p = await _repo.GetByIdAsync(id, ct);
        return p is null ? null : ProductDtoMappers.ToDto(p);
    }

    public async Task<ProductDto> CreateAsync(string name, string sourceUrl,
        ProductSource source, string? sku, string? hsCode,
        Guid? brandId, Guid? categoryId, bool isActive, CancellationToken ct)
    {
        var product = Product.Create(name, sourceUrl, source, sku, hsCode, brandId, categoryId, isActive);
        await _repo.AddAsync(product, ct);
        return ProductDtoMappers.ToDto(product);
    }

    public async Task<ProductDto?> UpdateAsync(Guid id, string? name, string? sku, string? hsCode,
        Guid? brandId, Guid? categoryId, bool? isActive, CancellationToken ct)
    {
        var product = await _repo.GetByIdAsync(id, ct);
        if (product is null) return null;

        if (name != null) product.Name = name;
        if (sku != null) product.Sku = sku;
        if (hsCode != null) product.HsCode = hsCode;
        if (brandId.HasValue) product.BrandId = brandId;
        if (categoryId.HasValue) product.CategoryId = categoryId;
        if (isActive.HasValue) product.IsActive = isActive.Value;

        await _repo.UpdateAsync(product, ct);
        return ProductDtoMappers.ToDto(product);
    }

    public async Task<PriceHistoryDto> GetPriceHistoryAsync(Guid productId,
        DateTime? from, DateTime? to, int limit, CancellationToken ct)
    {
        var product = await _repo.GetByIdAsync(productId, ct);
        var snapshots = await _repo.GetPriceHistoryAsync(productId, from, to, limit, ct);
        return new PriceHistoryDto(
            productId,
            product?.Name ?? "Unknown",
            snapshots.FirstOrDefault()?.Currency ?? "USD",
            snapshots.Select(ProductDtoMappers.ToSnapshotDto).ToList());
    }

    public async Task<ProductDto> UpsertFromScrapeAsync(string name, string? brand, string? sku,
        decimal price, string currency, decimal quantityPerUnit, string? sellerName,
        decimal? sellerRating, int? salesVolume, string sourceUrl,
        ProductSource source, string? hsCode, string? categoryName,
        CancellationToken ct)
    {
        // Get or create brand
        Guid? brandId = null;
        if (!string.IsNullOrWhiteSpace(brand))
        {
            var b = await _repo.GetOrCreateBrandAsync(brand, ct);
            brandId = b?.Id;
        }

        // Get or create category
        Guid? categoryId = null;
        if (!string.IsNullOrWhiteSpace(categoryName) && hsCode != null && !string.IsNullOrWhiteSpace(hsCode))
        {
            var c = await _repo.GetOrCreateCategoryAsync(categoryName, hsCode, ct);
            categoryId = c?.Id;
        }

        // Find existing product by URL or create new
        var existing = await _repo.FindBySourceUrlAsync(sourceUrl, ct);

        if (existing == null)
        {
            existing = Product.Create(name, sourceUrl, source, sku, hsCode, brandId, categoryId, true);
            await _repo.AddAsync(existing, ct);
        }

        // Insert snapshot directly — avoids re-loading the tracked entity (prevents DbUpdateConcurrencyException)
        var snapshot = PriceSnapshot.Create(
            existing.Id, price, currency, quantityPerUnit,
            sellerName, sellerRating, salesVolume);
        await _repo.AddPriceSnapshotDirectAsync(snapshot, ct);

        return ProductDtoMappers.ToDto(existing);
    }
}
