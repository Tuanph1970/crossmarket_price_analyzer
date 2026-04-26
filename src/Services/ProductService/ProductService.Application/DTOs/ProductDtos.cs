using Common.Domain.Enums;

namespace ProductService.Application.DTOs;

// Products
public record ProductDto(
    Guid Id,
    string Name,
    Guid? BrandId,
    string? BrandName,
    string? Sku,
    Guid? CategoryId,
    string? CategoryName,
    string? HsCode,
    ProductSource Source,
    string SourceUrl,
    bool IsActive,
    DateTime CreatedAt,
    PriceSnapshotDto? LatestSnapshot
);

public record ProductListDto(
    Guid Id,
    string Name,
    string? BrandName,
    string? CategoryName,
    ProductSource Source,
    string? LatestPrice,
    string? Currency,
    DateTime CreatedAt
);

public record PaginatedProductsDto(
    IReadOnlyList<ProductListDto> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages
);

// Price Snapshots
public record PriceSnapshotDto(
    Guid Id,
    Guid ProductId,
    decimal Price,
    string Currency,
    decimal UnitPrice,
    decimal QuantityPerUnit,
    string? SellerName,
    decimal? SellerRating,
    int? SalesVolume,
    DateTime ScrapedAt
);

public record PriceHistoryDto(
    Guid ProductId,
    string ProductName,
    string Currency,
    IReadOnlyList<PriceSnapshotDto> Snapshots
);

// Exchange Rates
public record ExchangeRateDto(
    Guid Id,
    string FromCurrency,
    string ToCurrency,
    decimal Rate,
    DateTime FetchedAt
);

// Categories
public record CategoryDto(
    Guid Id,
    string Name,
    string HsCode,
    Guid? ParentCategoryId,
    string? ParentCategoryName,
    int ProductCount
);
