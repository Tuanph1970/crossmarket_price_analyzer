using Common.Domain.Enums;

namespace MatchingService.Application.DTOs;

public record ProductMatchDto(
    Guid Id,
    Guid UsProductId,
    Guid VnProductId,
    decimal ConfidenceScore,
    ConfidenceLevel ConfidenceLevel,
    MatchStatus Status,
    string? ConfirmedBy,
    DateTime? ConfirmedAt,
    DateTime CreatedAt,
    List<MatchConfirmationDto>? Confirmations = null
);

public record MatchConfirmationDto(
    Guid Id,
    Guid MatchId,
    string UserId,
    string Action,
    string? Notes
);

public record CreateMatchRequest(
    Guid UsProductId,
    Guid VnProductId,
    string? UsProductName = null,
    string? VnProductName = null,
    string? UsBrand = null,
    string? VnBrand = null
);

public record BatchReviewRequest(
    List<BatchReviewItem> Items
);

public record BatchReviewItem(
    Guid MatchId,
    string Action  // "Confirm" or "Reject"
);

public record PaginatedMatchesDto(
    IReadOnlyList<ProductMatchDto> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages
);