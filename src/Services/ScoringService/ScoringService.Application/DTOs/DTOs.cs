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

/// <summary>
/// P3: Request body for POST /api/scores/export/excel.
/// Controls the title and row limit of the exported workbook.
/// </summary>
public record ExcelExportRequest(
    /// <summary>Sheet/workbook title. Defaults to "Opportunity Scores Export".</summary>
    string? Title = null,
    /// <summary>Maximum number of rows to export. Defaults to all (0 = unlimited). Use 20 for a Top-20 export.</summary>
    int Limit = 0
);