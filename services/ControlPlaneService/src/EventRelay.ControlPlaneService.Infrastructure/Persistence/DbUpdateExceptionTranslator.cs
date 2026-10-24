using EventRelay.ControlPlaneService.Infrastructure.Persistence.Exceptions.Abstraction;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace EventRelay.ControlPlaneService.Infrastructure.Persistence;

public sealed class DbUpdateExceptionTranslator
{
    private readonly IReadOnlyDictionary<string, IDbUpdateExceptionHandler> _handlers;

    public DbUpdateExceptionTranslator(IEnumerable<IDbUpdateExceptionHandler> handlers)
    {
        _handlers = handlers.ToDictionary(
            x => x.ConstraintName,
            StringComparer.Ordinal);
    }

    public Exception? Translate(DbUpdateException exception)
    {
        if (exception.InnerException is not PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation,
                ConstraintName: { } constraint
            })
        {
            return null;
        }

        return _handlers.TryGetValue(constraint, out var handler)
            ? handler.Handle(exception)
            : null;
    }
}
