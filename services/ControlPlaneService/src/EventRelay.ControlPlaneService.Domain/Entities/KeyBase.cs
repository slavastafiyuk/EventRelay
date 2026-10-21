using EventRelay.ControlPlaneService.Domain.Interfaces;

namespace EventRelay.ControlPlaneService.Domain.Entities;

public abstract class KeyBase<TSelf>
    : IKeyBase, IEquatable<TSelf>
    where TSelf : KeyBase<TSelf>
{
    public Guid Id { get; private set; }

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

    public override bool Equals(object? obj)
        => obj is TSelf other && Equals(other);

    public override int GetHashCode()
        => HashCode.Combine(GetType(), Id);

    public static bool operator ==(
        KeyBase<TSelf>? left,
        KeyBase<TSelf>? right)
        => object.Equals(left, right);

    public static bool operator !=(
        KeyBase<TSelf>? left,
        KeyBase<TSelf>? right)
        => !(left == right);
}
