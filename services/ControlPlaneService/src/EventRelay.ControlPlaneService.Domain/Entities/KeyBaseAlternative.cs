using EventRelay.ControlPlaneService.Domain.Interfaces;

namespace EventRelay.ControlPlaneService.Domain.Entities;

public abstract class KeyBaseAlternative<TSelf, TSecondary>
    : KeyBase<TSelf>, ISecondaryKey<TSecondary>
    where TSelf : KeyBaseAlternative<TSelf, TSecondary>
    where TSecondary : notnull
{
    public abstract TSecondary SecondaryKey { get; }

    protected KeyBaseAlternative() { }

    protected KeyBaseAlternative(Guid id) : base(id) { }

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
}
