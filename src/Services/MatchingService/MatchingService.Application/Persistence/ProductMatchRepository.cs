using Common.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using MatchingService.Domain.Entities;

namespace MatchingService.Application.Persistence;

/// <summary>
/// Repository for ProductMatch aggregate. Concrete implementation — not exposed via interface
/// to avoid circular dependency issues between Application and Infrastructure layers.
/// </summary>
public class ProductMatchRepository
{
    private readonly MatchingDbContext _context;

    public ProductMatchRepository(MatchingDbContext context)
    {
        _context = context;
    }

    public async Task<ProductMatch?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.ProductMatches
            .Include(m => m.Confirmations)
            .FirstOrDefaultAsync(m => m.Id == id, ct);
    }

    public async Task<IReadOnlyList<ProductMatch>> GetAllAsync(CancellationToken ct = default)
    {
        return await _context.ProductMatches
            .OrderByDescending(m => m.ConfidenceScore)
            .ToListAsync(ct);
    }

    public async Task<ProductMatch> AddAsync(ProductMatch entity, CancellationToken ct = default)
    {
        await _context.ProductMatches.AddAsync(entity, ct);
        await _context.SaveChangesAsync(ct);
        return entity;
    }

    public async Task UpdateAsync(ProductMatch entity, CancellationToken ct = default)
    {
        _context.ProductMatches.Update(entity);
        await _context.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(ProductMatch entity, CancellationToken ct = default)
    {
        _context.ProductMatches.Remove(entity);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.ProductMatches.AnyAsync(m => m.Id == id, ct);
    }

    public async Task<int> CountAsync(CancellationToken ct = default)
    {
        return await _context.ProductMatches.CountAsync(ct);
    }

    public async Task<IReadOnlyList<ProductMatch>> GetByUsProductIdAsync(
        Guid usProductId, CancellationToken ct = default)
    {
        return await _context.ProductMatches
            .Where(m => m.UsProductId == usProductId)
            .OrderByDescending(m => m.ConfidenceScore)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ProductMatch>> GetByVnProductIdAsync(
        Guid vnProductId, CancellationToken ct = default)
    {
        return await _context.ProductMatches
            .Where(m => m.VnProductId == vnProductId)
            .OrderByDescending(m => m.ConfidenceScore)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ProductMatch>> GetByStatusAsync(
        MatchStatus status, CancellationToken ct = default)
    {
        return await _context.ProductMatches
            .Where(m => m.Status == status)
            .OrderByDescending(m => m.ConfidenceScore)
            .ToListAsync(ct);
    }

    public async Task<(IReadOnlyList<ProductMatch> Items, int TotalCount)> GetPaginatedAsync(
        int page,
        int pageSize,
        MatchStatus? status = null,
        decimal? minScore = null,
        CancellationToken ct = default)
    {
        var query = _context.ProductMatches.AsQueryable();

        if (status.HasValue)
            query = query.Where(m => m.Status == status.Value);

        if (minScore.HasValue)
            query = query.Where(m => m.ConfidenceScore >= minScore.Value);

        var total = await query.CountAsync(ct);
        var items = await query
            .Include(m => m.Confirmations)
            .OrderByDescending(m => m.ConfidenceScore)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }
}
