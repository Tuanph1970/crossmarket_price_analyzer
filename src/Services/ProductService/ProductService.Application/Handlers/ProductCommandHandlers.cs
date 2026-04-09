using ProductService.Application.Commands;
using ProductService.Application.DTOs;
using ProductService.Application.Persistence;
using ProductService.Domain.Entities;

namespace ProductService.Application.Handlers;

public class CreateProductHandler : MediatR.IRequestHandler<CreateProductCommand, ProductDto>
{
    private readonly ProductRepository _repo;

    public CreateProductHandler(ProductRepository repo) { _repo = repo; }

    public async Task<ProductDto> Handle(CreateProductCommand cmd, CancellationToken ct)
    {
        var product = Product.Create(cmd.Name, cmd.SourceUrl, cmd.Source,
            cmd.Sku, cmd.HsCode, cmd.BrandId, cmd.CategoryId, cmd.IsActive);
        await _repo.AddAsync(product, ct);
        return ProductDtoMappers.ToDto(product);
    }
}

public class UpdateProductHandler : MediatR.IRequestHandler<UpdateProductCommand, ProductDto?>
{
    private readonly ProductRepository _repo;

    public UpdateProductHandler(ProductRepository repo) { _repo = repo; }

    public async Task<ProductDto?> Handle(UpdateProductCommand cmd, CancellationToken ct)
    {
        var product = await _repo.GetByIdAsync(cmd.Id, ct);
        if (product is null) return null;

        if (cmd.Name != null) product.Name = cmd.Name;
        if (cmd.Sku != null) product.Sku = cmd.Sku;
        if (cmd.HsCode != null) product.HsCode = cmd.HsCode;
        if (cmd.BrandId.HasValue) product.BrandId = cmd.BrandId;
        if (cmd.CategoryId.HasValue) product.CategoryId = cmd.CategoryId;
        if (cmd.IsActive.HasValue) product.IsActive = cmd.IsActive.Value;

        await _repo.UpdateAsync(product, ct);
        return ProductDtoMappers.ToDto(product);
    }
}

public class AddPriceSnapshotHandler : MediatR.IRequestHandler<AddPriceSnapshotCommand, PriceSnapshotDto>
{
    private readonly ProductRepository _repo;

    public AddPriceSnapshotHandler(ProductRepository repo) { _repo = repo; }

    public async Task<PriceSnapshotDto> Handle(AddPriceSnapshotCommand cmd, CancellationToken ct)
    {
        var snapshot = PriceSnapshot.Create(
            cmd.ProductId, cmd.Price, cmd.Currency, cmd.QuantityPerUnit,
            cmd.SellerName, cmd.SellerRating, cmd.SalesVolume);
        await _repo.AddPriceSnapshotAsync(cmd.ProductId, snapshot, ct);
        return ProductDtoMappers.ToSnapshotDto(snapshot);
    }
}

public class UpsertProductFromScrapeHandler : MediatR.IRequestHandler<UpsertProductFromScrapeCommand, ProductDto>
{
    private readonly ProductRepository _repo;

    public UpsertProductFromScrapeHandler(ProductRepository repo) { _repo = repo; }

    public async Task<ProductDto> Handle(UpsertProductFromScrapeCommand cmd, CancellationToken ct)
    {
        // Get or create brand
        Guid? brandId = null;
        if (!string.IsNullOrWhiteSpace(cmd.Brand))
        {
            var b = await _repo.GetOrCreateBrandAsync(cmd.Brand, ct);
            brandId = b?.Id;
        }

        // Get or create category
        Guid? categoryId = null;
        if (!string.IsNullOrWhiteSpace(cmd.CategoryName) && !string.IsNullOrWhiteSpace(cmd.HsCode))
        {
            var c = await _repo.GetOrCreateCategoryAsync(cmd.CategoryName, cmd.HsCode, ct);
            categoryId = c?.Id;
        }

        // Find existing by source URL, or create new
        var all = await _repo.GetAllAsync(ct);
        var existing = all.FirstOrDefault(p => p.SourceUrl == cmd.SourceUrl);

        if (existing == null)
        {
            existing = Product.Create(cmd.Name, cmd.SourceUrl, cmd.Source,
                cmd.Sku, cmd.HsCode, brandId, categoryId, true);
            await _repo.AddAsync(existing, ct);
        }

        // Add price snapshot
        var snapshot = PriceSnapshot.Create(
            existing.Id, cmd.Price, cmd.Currency, cmd.QuantityPerUnit,
            cmd.SellerName, cmd.SellerRating, cmd.SalesVolume);
        existing.PriceSnapshots.Add(snapshot);
        await _repo.UpdateAsync(existing, ct);

        return ProductDtoMappers.ToDto(existing);
    }
}

// DTO mappers — accessible to ProductServiceImpl via shared namespace
public static class ProductDtoMappers
{
    public static ProductDto ToDto(Product p) =>
        new(
            p.Id, p.Name, p.BrandId,
            p.Brand?.Name, p.Sku, p.CategoryId,
            p.Category?.Name, p.HsCode, p.Source,
            p.SourceUrl, p.IsActive, p.CreatedAt,
            p.PriceSnapshots?.OrderByDescending(s => s.ScrapedAt).FirstOrDefault() is { } s
                ? ToSnapshotDto(s) : null);

    public static ProductListDto ToListDto(Product p)
    {
        var latest = p.PriceSnapshots?.OrderByDescending(s => s.ScrapedAt).FirstOrDefault();
        return new ProductListDto(
            p.Id, p.Name, p.Brand?.Name, p.Category?.Name,
            p.Source,
            latest != null ? latest.Price.ToString("N0") : null,
            latest?.Currency, p.CreatedAt);
    }

    public static PriceSnapshotDto ToSnapshotDto(PriceSnapshot s) =>
        new(s.Id, s.ProductId, s.Price, s.Currency, s.UnitPrice,
            s.QuantityPerUnit, s.SellerName, s.SellerRating, s.SalesVolume, s.ScrapedAt);
}
