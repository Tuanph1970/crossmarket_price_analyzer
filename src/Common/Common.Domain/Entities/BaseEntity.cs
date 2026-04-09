namespace Common.Domain.Entities;

/// <summary>
/// Base class for all entities with value-based equality semantics.
/// </summary>
/// <typeparam name="TId">The type of the entity's primary key.</typeparam>
public abstract class BaseEntity<TId> where TId : notnull
{
    public TId Id { get; protected internal set; } = default!;

    public override bool Equals(object? obj)
    {
        if (obj is not BaseEntity<TId> other)
            return false;

        if (ReferenceEquals(this, other))
            return true;

        if (GetType() != other.GetType())
            return false;

        return Id!.Equals(other.Id);
    }

    public override int GetHashCode() => Id!.GetHashCode();

    public static bool operator ==(BaseEntity<TId>? left, BaseEntity<TId>? right)
    {
        if (left is null && right is null) return true;
        if (left is null || right is null) return false;
        return left.Equals(right);
    }

    public static bool operator !=(BaseEntity<TId>? left, BaseEntity<TId>? right)
        => !(left == right);
}
