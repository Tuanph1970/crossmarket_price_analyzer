using ProductService.Application.DTOs;
using ProductService.Application.Handlers;
using ProductService.Application.Persistence;
using ProductService.Application.Queries;

namespace ProductService.Application.Handlers;

public class GetProductsHandler : MediatR.IRequestHandler<GetProductsQuery, PaginatedProductsDto>
{
    private readonly ProductRepository _repo;

    public GetProductsHandler(ProductRepository repo) { _repo = repo; }

    public async Task<PaginatedProductsDto> Handle(GetProductsQuery q, CancellationToken ct)
    {
        var (items, total) = await _repo.GetPaginatedAsync(
            q.Page, q.PageSize, q.Source, q.CategoryId, q.IsActive, ct);

        return new PaginatedProductsDto(
            items.Select(ProductDtoMappers.ToListDto).ToList(),
            total, q.Page, q.PageSize,
            (int)Math.Ceiling(total / (double)q.PageSize));
    }
}

public class GetProductByIdHandler : MediatR.IRequestHandler<GetProductByIdQuery, ProductDto?>
{
    private readonly ProductRepository _repo;

    public GetProductByIdHandler(ProductRepository repo) { _repo = repo; }

    public async Task<ProductDto?> Handle(GetProductByIdQuery q, CancellationToken ct)
    {
        var p = await _repo.GetByIdAsync(q.Id, ct);
        return p is null ? null : ProductDtoMappers.ToDto(p);
    }
}

public class GetPriceHistoryHandler : MediatR.IRequestHandler<GetPriceHistoryQuery, PriceHistoryDto>
{
    private readonly ProductRepository _repo;

    public GetPriceHistoryHandler(ProductRepository repo) { _repo = repo; }

    public async Task<PriceHistoryDto> Handle(GetPriceHistoryQuery q, CancellationToken ct)
    {
        var product = await _repo.GetByIdAsync(q.ProductId, ct);
        var snapshots = await _repo.GetPriceHistoryAsync(q.ProductId, q.From, q.To, q.Limit, ct);

        return new PriceHistoryDto(
            q.ProductId,
            product?.Name ?? "Unknown",
            snapshots.FirstOrDefault()?.Currency ?? "USD",
            snapshots.Select(ProductDtoMappers.ToSnapshotDto).ToList());
    }
}

public class GetExchangeRateHandler : MediatR.IRequestHandler<GetExchangeRateQuery, ExchangeRateDto?>
{
    private readonly ProductRepository _repo;

    public GetExchangeRateHandler(ProductRepository repo) { _repo = repo; }

    public async Task<ExchangeRateDto?> Handle(GetExchangeRateQuery q, CancellationToken ct)
    {
        var rate = await _repo.GetLatestRateAsync(q.From, q.To, ct);
        if (rate == null) return null;
        return new ExchangeRateDto(rate.Id, rate.FromCurrency, rate.ToCurrency, rate.Rate, rate.FetchedAt);
    }
}
