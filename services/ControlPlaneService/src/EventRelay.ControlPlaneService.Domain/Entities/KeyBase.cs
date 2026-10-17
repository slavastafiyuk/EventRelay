using EventRelay.ControlPlaneService.Domain.Interfaces;

namespace EventRelay.ControlPlaneService.Domain.Entities;

public abstract class KeyBase<TSelf, TSecondary>
    : IKeyBase,
      ISecondaryKey<TSecondary>,
      IEquatable<TSelf>
    where TSelf : KeyBase<TSelf, TSecondary>
    where TSecondary : notnull
{
    public Guid Id { get; private set; }

    public abstract TSecondary SecondaryKey { get; }

    protected KeyBase() => Id = Guid.CreateVersion7();

    protected KeyBase(Guid id)
    {
        if (id == Guid.Empty)
            throw new ArgumentException(
                "Id cannot be empty.", nameof(id));

        Id = id;
    }

    public bool Equals(TSelf? other)
        => ReferenceEquals(this, other)
           || (other is not null
               && GetType() == other.GetType()
               && Id != Guid.Empty
               && Id == other.Id);

    public bool MatchesSecondaryKey(
        TSelf? other,
        IEqualityComparer<TSecondary>? comparer = null)
    {
        if (other is null || GetType() != other.GetType())
            return false;

        comparer ??= EqualityComparer<TSecondary>.Default;

        return comparer.Equals(
            SecondaryKey,
            other.SecondaryKey);
    }

    public override bool Equals(object? obj)
        => obj is TSelf other && Equals(other);

    public override int GetHashCode()
        => HashCode.Combine(GetType(), Id);

    public static bool operator ==(
        KeyBase<TSelf, TSecondary>? left,
        KeyBase<TSelf, TSecondary>? right)
        => object.Equals(left, right);

    public static bool operator !=(
        KeyBase<TSelf, TSecondary>? left,
        KeyBase<TSelf, TSecondary>? right)
        => !(left == right);
}
