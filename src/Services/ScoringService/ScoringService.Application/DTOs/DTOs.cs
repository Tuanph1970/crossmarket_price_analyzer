namespace ScoringService.Application.DTOs;

public record OpportunityScoreDto(
    Guid Id,
    Guid MatchId,
    decimal CompositeScore,
    decimal ProfitMarginPct,
    decimal Roi,
    decimal DemandScore,
    decimal CompetitionScore,
    decimal PriceStabilityScore,
    decimal MatchConfidenceScore,
    decimal LandedCostVnd,
    decimal VietnamRetailVnd,
    decimal PriceDifferenceVnd,
    DateTime CalculatedAt
);

public record ScoringBreakdownDto(
    Guid MatchId,
    decimal CompositeScore,
    List<FactorScoreDto> Factors,
    LandedCostBreakdownDto? CostBreakdown
);

public record FactorScoreDto(
    string FactorKey,
    decimal RawScore,
    decimal Weight,
    decimal NormalizedScore,
    decimal WeightedScore
);

public record LandedCostBreakdownDto(
    decimal UsPurchasePriceVnd,
    decimal ShippingCostVnd,
    decimal ImportDutyVnd,
    decimal VatVnd,
    decimal HandlingFeesVnd,
    decimal TotalLandedCostVnd
);

public record ScoringConfigDto(
    List<ScoringConfigItemDto> Weights
);

public record ScoringConfigItemDto(
    string FactorKey,
    decimal Weight,
    decimal MinThreshold,
    decimal MaxThreshold
);

public record PaginatedScoresDto(
    IReadOnlyList<OpportunityScoreDto> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages
);

public record UpdateWeightsRequest(
    List<ScoringConfigItemDto> Weights
);