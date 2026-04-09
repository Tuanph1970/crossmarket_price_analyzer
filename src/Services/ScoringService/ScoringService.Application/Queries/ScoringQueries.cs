using ScoringService.Application.DTOs;

namespace ScoringService.Application.Queries;

public record GetOpportunitiesQuery(
    int Page = 1,
    int PageSize = 20,
    decimal? MinMargin = null,
    string? Category = null,
    string? Source = null
) : MediatR.IRequest<PaginatedScoresDto>;

public record GetScoreByMatchIdQuery(Guid MatchId) : MediatR.IRequest<ScoringBreakdownDto?>;

public record GetScoringConfigQuery : MediatR.IRequest<ScoringConfigDto>;