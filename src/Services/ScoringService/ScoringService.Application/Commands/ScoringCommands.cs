using ScoringService.Application.DTOs;

namespace ScoringService.Application.Commands;

public record CalculateScoreCommand(
    Guid MatchId,
    decimal UsPriceUsd,
    decimal VnRetailPriceVnd,
    decimal DemandScore,
    decimal CompetitionScore,
    decimal PriceStabilityScore,
    decimal MatchConfidenceScore,
    decimal ExchangeRate = 25000m,
    decimal? ShippingCostUsd = null,
    decimal? ImportDutyRatePct = null,
    decimal? VatRatePct = null
) : MediatR.IRequest<ScoringBreakdownDto>;

public record UpdateWeightsCommand(
    List<ScoringConfigItemDto> Weights
) : MediatR.IRequest<bool>;

public record RecalculateAllScoresCommand(
    int BatchSize = 100
) : MediatR.IRequest<int>;  // Returns number of recalculated scores