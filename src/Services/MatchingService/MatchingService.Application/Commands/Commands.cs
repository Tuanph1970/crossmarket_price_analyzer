using Common.Domain.Enums;
using MatchingService.Application.DTOs;
using MatchingService.Domain.Entities;

namespace MatchingService.Application.Commands;

public record CreateMatchCommand(
    Guid UsProductId,
    Guid VnProductId,
    string? UsProductName = null,
    string? VnProductName = null,
    string? UsBrand = null,
    string? VnBrand = null
) : MediatR.IRequest<ProductMatchDto>;

public record ConfirmMatchCommand(
    Guid MatchId,
    string UserId,
    string? Notes = null
) : MediatR.IRequest<bool>;

public record RejectMatchCommand(
    Guid MatchId,
    string UserId,
    string? Notes = null
) : MediatR.IRequest<bool>;

public record BatchReviewCommand(
    List<BatchReviewItem> Items,
    string UserId
) : MediatR.IRequest<int>;  // Returns number of processed items