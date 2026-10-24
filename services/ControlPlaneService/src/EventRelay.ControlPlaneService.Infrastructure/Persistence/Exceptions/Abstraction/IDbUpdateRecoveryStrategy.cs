using Microsoft.EntityFrameworkCore;

namespace EventRelay.ControlPlaneService.Infrastructure.Persistence.Exceptions.Abstraction;

public interface IDbUpdateRecoveryStrategy
{
    string ConstraintName { get; }
    bool TryRecover(DbUpdateException exception);
}
