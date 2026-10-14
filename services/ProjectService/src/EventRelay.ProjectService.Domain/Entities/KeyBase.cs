using EventRelay.ProjectService.Domain.Interfaces;

namespace EventRelay.ProjectService.Domain.Entities;

public abstract class KeyBase<TSelf> : IKeyBase, IEquatable<TSelf>
    where TSelf : KeyBase<TSelf>
{
    public abstract Guid Id { get; set; }

    public bool Equals(TSelf? other)
        => other is not null && Id == other.Id;

    public override bool Equals(object? obj)
        => obj is TSelf other && Equals(other);

    public override int GetHashCode()
        => Id.GetHashCode();

}
