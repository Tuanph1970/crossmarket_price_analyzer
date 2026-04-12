namespace AuthService.Application.DTOs;

// ── Requests ──────────────────────────────────────────────────────────────────

public record RegisterRequest(
    string Email,
    string Password,
    string FullName
);

public record LoginRequest(
    string Email,
    string Password
);

public record RefreshTokenRequest(
    string RefreshToken
);

public record AddToWatchlistRequest(
    Guid MatchId,
    string UsProductName,
    string VnProductName,
    decimal? AlertAboveScore = null,
    decimal? AlertBelowScore = null
);

public record CreateAlertThresholdRequest(
    string Name,
    decimal MinScore,
    decimal? MaxScore = null,
    decimal? MinMarginPct = null,
    Guid? MatchId = null
);

public record UpdateAlertThresholdRequest(
    decimal MinScore,
    decimal? MaxScore = null,
    decimal? MinMarginPct = null
);

// ── Responses ────────────────────────────────────────────────────────────────

public record AuthResponse(
    string AccessToken,
    string RefreshToken,
    DateTime ExpiresAt,
    UserDto User
);

public record UserDto(
    Guid Id,
    string Email,
    string FullName,
    bool IsEmailConfirmed
);

public record WatchlistItemDto(
    Guid Id,
    Guid MatchId,
    string UsProductName,
    string VnProductName,
    decimal? AlertAboveScore,
    decimal? AlertBelowScore,
    bool IsMuted,
    DateTime CreatedAt
);

public record AlertThresholdDto(
    Guid Id,
    string Name,
    decimal MinScore,
    decimal? MaxScore,
    decimal? MinMarginPct,
    bool IsActive,
    DateTime CreatedAt
);

public record PagedResult<T>(
    IReadOnlyList<T> Items,
    int TotalCount,
    int Page,
    int PageSize
);
