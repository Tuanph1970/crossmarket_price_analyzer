using Common.Domain.Enums;
using ProductService.Application.DTOs;

namespace ProductService.Application.Commands;

public record CreateProductCommand(
    string Name,
    string SourceUrl,
    ProductSource Source,
    string? Sku = null,
    string? HsCode = null,
    Guid? BrandId = null,
    Guid? CategoryId = null,
    bool IsActive = true
) : MediatR.IRequest<ProductDto>;

public record UpdateProductCommand(
    Guid Id,
    string? Name = null,
    string? Sku = null,
    string? HsCode = null,
    Guid? BrandId = null,
    Guid? CategoryId = null,
    bool? IsActive = null
) : MediatR.IRequest<ProductDto?>;

public record AddPriceSnapshotCommand(
    Guid ProductId,
    decimal Price,
    string Currency,
    decimal UnitPrice,
    decimal QuantityPerUnit,
    string? SellerName,
    decimal? SellerRating,
    int? SalesVolume
) : MediatR.IRequest<PriceSnapshotDto>;

public record UpsertProductFromScrapeCommand(
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
    ProductSource Source,
    string? HsCode,
    string? CategoryName
) : MediatR.IRequest<ProductDto>;
