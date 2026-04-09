using Common.Domain.Enums;
using MatchingService.Application.DTOs;
using MatchingService.Application.Persistence;
using MatchingService.Application.Queries;
using MatchingService.Domain.Entities;

namespace MatchingService.Application.Handlers;

public class GetMatchesHandler : MediatR.IRequestHandler<GetMatchesQuery, PaginatedMatchesDto>
{
    private readonly ProductMatchRepository _repository;

    public GetMatchesHandler(ProductMatchRepository repository)
    {
        _repository = repository;
    }

    public async Task<PaginatedMatchesDto> Handle(GetMatchesQuery request, CancellationToken ct)
    {
        var (items, total) = await _repository.GetPaginatedAsync(
            request.Page,
            request.PageSize,
            request.Status,
            request.MinScore,
            ct);

        return new PaginatedMatchesDto(
            items.Select(ToDto).ToList(),
            total,
            request.Page,
            request.PageSize,
            (int)Math.Ceiling(total / (double)request.PageSize)
        );
    }

    private static ProductMatchDto ToDto(ProductMatch m) =>
        new(
            m.Id,
            m.UsProductId,
            m.VnProductId,
            m.ConfidenceScore,
            m.GetConfidenceLevel(),
            m.Status,
            m.ConfirmedBy,
            m.ConfirmedAt,
            m.CreatedAt,
            m.Confirmations?.Select(c => new MatchConfirmationDto(
                c.Id, c.MatchId, c.UserId, c.Action.ToString(), c.Notes
            )).ToList()
        );
}

public class GetMatchByIdHandler : MediatR.IRequestHandler<GetMatchByIdQuery, ProductMatchDto?>
{
    private readonly ProductMatchRepository _repository;

    public GetMatchByIdHandler(ProductMatchRepository repository)
    {
        _repository = repository;
    }

    public async Task<ProductMatchDto?> Handle(GetMatchByIdQuery request, CancellationToken ct)
    {
        var match = await _repository.GetByIdAsync(request.MatchId, ct);
        return match is null ? null : new ProductMatchDto(
            match.Id,
            match.UsProductId,
            match.VnProductId,
            match.ConfidenceScore,
            match.GetConfidenceLevel(),
            match.Status,
            match.ConfirmedBy,
            match.ConfirmedAt,
            match.CreatedAt,
            match.Confirmations?.Select(c => new MatchConfirmationDto(
                c.Id, c.MatchId, c.UserId, c.Action.ToString(), c.Notes
            )).ToList()
        );
    }
}
