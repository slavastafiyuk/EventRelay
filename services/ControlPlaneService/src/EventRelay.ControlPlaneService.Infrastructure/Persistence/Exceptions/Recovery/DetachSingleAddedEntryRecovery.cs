using EventRelay.ControlPlaneService.Infrastructure.Persistence.Exceptions.Abstraction;
using Microsoft.EntityFrameworkCore;

namespace EventRelay.ControlPlaneService.Infrastructure.Persistence.Exceptions.Recovery;

public sealed class DetachSingleAddedEntryRecovery<TEntity>(string constraintName) : IDbUpdateRecoveryStrategy
    where TEntity : class
{
    public string ConstraintName { get; } = constraintName;
    public bool TryRecover(DbUpdateException exception)
    {
        if (exception.Entries.Count != 1) return false;

        var entry = exception.Entries[0];

        if (entry.Entity is not TEntity || entry.State != EntityState.Added) return false;

        entry.State = EntityState.Detached;

        return true;
    }
}
