using Common.Domain.Enums;
using MatchingService.Application.DTOs;

namespace MatchingService.Application.Queries;

public record GetMatchesQuery(
    int Page = 1,
    int PageSize = 20,
    MatchStatus? Status = null,
    decimal? MinScore = null
) : MediatR.IRequest<PaginatedMatchesDto>;

public record GetMatchByIdQuery(Guid MatchId) : MediatR.IRequest<ProductMatchDto?>;