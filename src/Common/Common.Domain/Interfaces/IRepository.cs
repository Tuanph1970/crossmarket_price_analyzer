namespace Common.Domain.Interfaces;

/// <summary>
/// Generic repository contract for aggregate persistence.
/// </summary>
/// <typeparam name="T">The aggregate root entity type.</typeparam>
public interface IRepository<T> where T : class
{
    Task<T?> GetByIdAsync<TId>(TId id, CancellationToken ct = default) where TId : notnull;
    Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default);
    Task<T> AddAsync(T entity, CancellationToken ct = default);
    Task UpdateAsync(T entity, CancellationToken ct = default);
    Task DeleteAsync(T entity, CancellationToken ct = default);
    Task<bool> ExistsAsync<TId>(TId id, CancellationToken ct = default) where TId : notnull;
    Task<int> CountAsync(CancellationToken ct = default);
}
