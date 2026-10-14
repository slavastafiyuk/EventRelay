using EventRelay.ProjectService.Domain.Interfaces;

namespace EventRelay.ProjectService.Domain.Entities;

public abstract class KeyBase<TSelf> : IKeyBase, IEquatable<TSelf>
    where TSelf : KeyBase<TSelf>
{
    public Guid Id { get; set; }

    public bool Equals(TSelf? other)
        => other is not null
           && GetType() == other.GetType()
           && Id != Guid.Empty
           && Id == other.Id;

    public override bool Equals(object? obj)
        => obj is TSelf other && Equals(other);

    public static bool operator ==(
        KeyBase<TSelf>? left,
        KeyBase<TSelf>? right)
        => ReferenceEquals(left, right)
           || (left is not null && left.Equals(right));

    public static bool operator !=(
        KeyBase<TSelf>? left,
        KeyBase<TSelf>? right)
        => !(left == right);

    public override int GetHashCode()
        => HashCode.Combine(GetType(), Id);

    protected KeyBase() => Id = Guid.CreateVersion7();

    public KeyBase(Guid id)
    {
        if (id == Guid.Empty)
            throw new ArgumentException(
                "Id cannot be empty.", nameof(id));

        Id = id;
    }

}
