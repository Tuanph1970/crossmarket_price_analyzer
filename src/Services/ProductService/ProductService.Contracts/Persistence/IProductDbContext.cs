using Microsoft.EntityFrameworkCore;
using ProductService.Domain.Entities;

namespace ProductService.Contracts.Persistence;

/// <summary>
/// Interface for the Product DbContext.
/// Defined in Contracts to break the Application ↔ Infrastructure circular dependency:
/// - Infrastructure implements this (references Application via Domain)
/// - Application references this (references Infrastructure via Contracts)
/// - Domain does NOT reference Contracts (no circular dependency chain)
/// </summary>
public interface IProductDbContext
{
    DbSet<Product> Products { get; }
    DbSet<Brand> Brands { get; }
    DbSet<Category> Categories { get; }
    DbSet<PriceSnapshot> PriceSnapshots { get; }
    DbSet<ExchangeRate> ExchangeRates { get; }
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
