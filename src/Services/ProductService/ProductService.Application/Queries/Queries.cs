using Common.Domain.Enums;
using ProductService.Application.DTOs;

namespace ProductService.Application.Queries;

public record GetProductsQuery(
    int Page = 1,
    int PageSize = 20,
    ProductSource? Source = null,
    Guid? CategoryId = null,
    bool? IsActive = null
) : MediatR.IRequest<PaginatedProductsDto>;

public record GetProductByIdQuery(Guid Id) : MediatR.IRequest<ProductDto?>;

public record GetPriceHistoryQuery(
    Guid ProductId,
    DateTime? From = null,
    DateTime? To = null,
    int Limit = 30
) : MediatR.IRequest<PriceHistoryDto>;

public record GetExchangeRateQuery(
    string From = "USD",
    string To = "VND"
) : MediatR.IRequest<ExchangeRateDto?>;
