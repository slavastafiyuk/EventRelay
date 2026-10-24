using Microsoft.EntityFrameworkCore;

namespace EventRelay.ControlPlaneService.Infrastructure.Persistence.Exceptions.Abstraction;

public interface IDbUpdateExceptionHandler
{
    string ConstraintName { get; }
    Exception Handle(DbUpdateException exception);
}
