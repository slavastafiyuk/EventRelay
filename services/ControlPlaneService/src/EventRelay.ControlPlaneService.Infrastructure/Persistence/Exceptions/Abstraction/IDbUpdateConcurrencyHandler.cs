
using Microsoft.EntityFrameworkCore;

namespace EventRelay.ControlPlaneService.Infrastructure.Persistence.Exceptions.Abstraction;

public interface IDbUpdateConcurrencyHandler
{
    bool CanHandle(DbUpdateConcurrencyException exception);
    Exception Handle(DbUpdateConcurrencyException exception);
}
