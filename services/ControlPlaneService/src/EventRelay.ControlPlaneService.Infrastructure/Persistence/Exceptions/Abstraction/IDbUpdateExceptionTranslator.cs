using Microsoft.EntityFrameworkCore;

namespace EventRelay.ControlPlaneService.Infrastructure.Persistence.Exceptions.Abstraction;

public interface IDbUpdateExceptionTranslator
{
    Exception? Translate(DbUpdateException exception);
}
