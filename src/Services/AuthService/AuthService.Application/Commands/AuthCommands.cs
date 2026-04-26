using AuthService.Application.DTOs;
using MediatR;

namespace AuthService.Application.Commands;

// ── Auth ─────────────────────────────────────────────────────────────────────

public record RegisterCommand(
    string Email,
    string Password,
    string FullName
) : IRequest<AuthResponse>;

public record LoginCommand(
    string Email,
    string Password
) : IRequest<AuthResponse>;

public record RefreshTokenCommand(string RefreshToken) : IRequest<AuthResponse>;

// ── Watchlist ────────────────────────────────────────────────────────────────

public record AddToWatchlistCommand(
    Guid UserId,
    Guid MatchId,
    string? UsProductName,
    string? VnProductName,
    decimal? AlertAboveScore = null,
    decimal? AlertBelowScore = null
) : IRequest<WatchlistItemDto>;

public record RemoveFromWatchlistCommand(Guid UserId, Guid ItemId) : IRequest<bool>;

public record GetWatchlistQuery(Guid UserId, int Page = 1, int PageSize = 20) : IRequest<PagedResult<WatchlistItemDto>>;

// ── Alert Thresholds ──────────────────────────────────────────────────────────

public record CreateAlertThresholdCommand(
    Guid UserId,
    string Name,
    decimal MinScore,
    decimal? MaxScore = null,
    decimal? MinMarginPct = null,
    Guid? MatchId = null
) : IRequest<AlertThresholdDto>;

public record UpdateAlertThresholdCommand(
    Guid UserId,
    Guid ThresholdId,
    decimal MinScore,
    decimal? MaxScore = null,
    decimal? MinMarginPct = null
) : IRequest<AlertThresholdDto>;

public record DeleteAlertThresholdCommand(Guid UserId, Guid ThresholdId) : IRequest<bool>;

public record GetAlertThresholdsQuery(Guid UserId) : IRequest<IReadOnlyList<AlertThresholdDto>>;
