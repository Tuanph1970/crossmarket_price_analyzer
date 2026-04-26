using AuthService.Application.Commands;
using AuthService.Application.DTOs;
using AuthService.Application.Persistence;
using AuthService.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AuthService.Application.Commands;

// ── Watchlist ─────────────────────────────────────────────────────────────────

public sealed class AddToWatchlistHandler : IRequestHandler<AddToWatchlistCommand, WatchlistItemDto>
{
    private readonly AuthDbContext _db;

    public AddToWatchlistHandler(AuthDbContext db) => _db = db;

    public async Task<WatchlistItemDto> Handle(AddToWatchlistCommand cmd, CancellationToken ct)
    {
        var item = WatchlistItem.Create(
            cmd.UserId, cmd.MatchId,
            cmd.UsProductName ?? string.Empty,
            cmd.VnProductName ?? string.Empty,
            cmd.AlertAboveScore, cmd.AlertBelowScore);

        await _db.WatchlistItems.AddAsync(item, ct);
        await _db.SaveChangesAsync(ct);

        return WatchlistDtoMappers.ToDto(item);
    }
}

public sealed class RemoveFromWatchlistHandler : IRequestHandler<RemoveFromWatchlistCommand, bool>
{
    private readonly AuthDbContext _db;

    public RemoveFromWatchlistHandler(AuthDbContext db) => _db = db;

    public async Task<bool> Handle(RemoveFromWatchlistCommand cmd, CancellationToken ct)
    {
        var item = await _db.WatchlistItems
            .FirstOrDefaultAsync(w => w.Id == cmd.ItemId && w.UserId == cmd.UserId, ct);
        if (item is null) return false;

        _db.WatchlistItems.Remove(item);
        await _db.SaveChangesAsync(ct);
        return true;
    }
}

public sealed class GetWatchlistHandler : IRequestHandler<GetWatchlistQuery, PagedResult<WatchlistItemDto>>
{
    private readonly AuthDbContext _db;

    public GetWatchlistHandler(AuthDbContext db) => _db = db;

    public async Task<PagedResult<WatchlistItemDto>> Handle(GetWatchlistQuery cmd, CancellationToken ct)
    {
        var q = _db.WatchlistItems
            .Where(w => w.UserId == cmd.UserId)
            .OrderByDescending(w => w.CreatedAt);

        var total = await q.CountAsync(ct);
        var items = await q
            .Skip((cmd.Page - 1) * cmd.PageSize)
            .Take(cmd.PageSize)
            .ToListAsync(ct);

        return new PagedResult<WatchlistItemDto>(
            items.Select(WatchlistDtoMappers.ToDto).ToList(), total, cmd.Page, cmd.PageSize);
    }
}

// ── Alert Thresholds ───────────────────────────────────────────────────────────

public sealed class CreateAlertThresholdHandler : IRequestHandler<CreateAlertThresholdCommand, AlertThresholdDto>
{
    private readonly AuthDbContext _db;

    public CreateAlertThresholdHandler(AuthDbContext db) => _db = db;

    public async Task<AlertThresholdDto> Handle(CreateAlertThresholdCommand cmd, CancellationToken ct)
    {
        var threshold = AlertThreshold.Create(
            cmd.UserId, cmd.Name, cmd.MinScore, cmd.MaxScore, cmd.MinMarginPct, cmd.MatchId);

        await _db.AlertThresholds.AddAsync(threshold, ct);
        await _db.SaveChangesAsync(ct);

        return WatchlistDtoMappers.ToDto(threshold);
    }
}

public sealed class UpdateAlertThresholdHandler : IRequestHandler<UpdateAlertThresholdCommand, AlertThresholdDto>
{
    private readonly AuthDbContext _db;

    public UpdateAlertThresholdHandler(AuthDbContext db) => _db = db;

    public async Task<AlertThresholdDto> Handle(UpdateAlertThresholdCommand cmd, CancellationToken ct)
    {
        var threshold = await _db.AlertThresholds
            .FirstOrDefaultAsync(a => a.Id == cmd.ThresholdId && a.UserId == cmd.UserId, ct)
            ?? throw new KeyNotFoundException($"Alert threshold {cmd.ThresholdId} not found.");

        threshold.UpdateThresholds(cmd.MinScore, cmd.MaxScore, cmd.MinMarginPct);
        await _db.SaveChangesAsync(ct);

        return WatchlistDtoMappers.ToDto(threshold);
    }
}

public sealed class DeleteAlertThresholdHandler : IRequestHandler<DeleteAlertThresholdCommand, bool>
{
    private readonly AuthDbContext _db;

    public DeleteAlertThresholdHandler(AuthDbContext db) => _db = db;

    public async Task<bool> Handle(DeleteAlertThresholdCommand cmd, CancellationToken ct)
    {
        var threshold = await _db.AlertThresholds
            .FirstOrDefaultAsync(a => a.Id == cmd.ThresholdId && a.UserId == cmd.UserId, ct);
        if (threshold is null) return false;

        threshold.Deactivate();
        await _db.SaveChangesAsync(ct);
        return true;
    }
}

public sealed class GetAlertThresholdsHandler : IRequestHandler<GetAlertThresholdsQuery, IReadOnlyList<AlertThresholdDto>>
{
    private readonly AuthDbContext _db;

    public GetAlertThresholdsHandler(AuthDbContext db) => _db = db;

    public async Task<IReadOnlyList<AlertThresholdDto>> Handle(GetAlertThresholdsQuery cmd, CancellationToken ct)
    {
        var items = await _db.AlertThresholds
            .Where(a => a.UserId == cmd.UserId)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(ct);

        return items.Select(WatchlistDtoMappers.ToDto).ToList();
    }
}

// ── DTO mappers ────────────────────────────────────────────────────────────────

public static class WatchlistDtoMappers
{
    public static WatchlistItemDto ToDto(WatchlistItem w) => new(
        w.Id, w.ProductMatchId, w.UsProductName, w.VnProductName,
        w.AlertAboveScore, w.AlertBelowScore, w.IsMuted, w.CreatedAt);

    public static AlertThresholdDto ToDto(AlertThreshold a) => new(
        a.Id, a.Name, a.MinScore, a.MaxScore, a.MinMarginPct, a.IsActive, a.CreatedAt);
}
