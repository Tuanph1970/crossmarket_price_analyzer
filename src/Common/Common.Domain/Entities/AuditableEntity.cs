namespace Common.Domain.Entities;

/// <summary>
/// Base class for all auditable entities with CreatedAt and UpdatedAt timestamps.
/// </summary>
/// <typeparam name="TId">The type of the entity's primary key.</typeparam>
public abstract class AuditableEntity<TId> : BaseEntity<TId> where TId : notnull
{
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
