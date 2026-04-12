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
    // P2-B06: optional overrides — when provided, LandedCostCalculator honours them
    decimal? ShippingCostUsd = null,
    decimal? ImportDutyRatePct = null,
    decimal? VatRatePct = null,
    /// <summary>Manual landed cost override — bypasses calculator when set.</summary>
    decimal? LandedCostOverrideVnd = null,
    /// <summary>
    /// Override for the import duty rate — when set, replaces the default lookup table value.
    /// </summary>
    decimal? ImportDutyOverridePct = null
) : MediatR.IRequest<ScoringBreakdownDto>;

public record UpdateWeightsCommand(
    List<ScoringConfigItemDto> Weights
) : MediatR.IRequest<bool>;

public record RecalculateAllScoresCommand(
    int BatchSize = 100
) : MediatR.IRequest<int>;  // Returns number of recalculated scores

/// <summary>
/// P2-B06: Request body for PUT /api/scores/manual-costs.
/// Allows callers to store manual landed-cost override values for a match
/// without re-running the full scoring engine.
/// </summary>
public record ManualCostOverrideRequest(
    Guid MatchId,
    /// <summary>Override the landed cost in VND. Recalculates margin from this value.</summary>
    decimal? LandedCostOverrideVnd = null,
    /// <summary>Override the shipping cost in USD.</summary>
    decimal? ShippingCostUsd = null,
    /// <summary>Override the import duty percentage.</summary>
    decimal? ImportDutyOverridePct = null
);
