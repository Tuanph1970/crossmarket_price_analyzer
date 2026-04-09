using ProductService.Application.DTOs;

namespace ProductService.Application.Services;

/// <summary>
/// Application-layer service interface for ProductService operations.
/// Infrastructure implements this; API references only Infrastructure (no App reference).
/// </summary>
public interface IProductService
{
    Task<PaginatedProductsDto> GetProductsAsync(int page, int pageSize,
        Common.Domain.Enums.ProductSource? source, Guid? categoryId, bool? isActive, CancellationToken ct);
    Task<ProductDto?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<ProductDto> CreateAsync(string name, string sourceUrl,
        Common.Domain.Enums.ProductSource source, string? sku, string? hsCode,
        Guid? brandId, Guid? categoryId, bool isActive, CancellationToken ct);
    Task<ProductDto?> UpdateAsync(Guid id, string? name, string? sku, string? hsCode,
        Guid? brandId, Guid? categoryId, bool? isActive, CancellationToken ct);
    Task<PriceHistoryDto> GetPriceHistoryAsync(Guid productId,
        DateTime? from, DateTime? to, int limit, CancellationToken ct);
    Task<ProductDto> UpsertFromScrapeAsync(string name, string? brand, string? sku,
        decimal price, string currency, decimal quantityPerUnit, string? sellerName,
        decimal? sellerRating, int? salesVolume, string sourceUrl,
        Common.Domain.Enums.ProductSource source, string? hsCode, string? categoryName,
        CancellationToken ct);
}
