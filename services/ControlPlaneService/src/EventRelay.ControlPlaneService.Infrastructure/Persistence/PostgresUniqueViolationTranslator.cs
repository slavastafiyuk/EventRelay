
using EventRelay.ControlPlaneService.Infrastructure.Persistence.Exceptions.Abstraction;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace EventRelay.ControlPlaneService.Infrastructure.Persistence;

public sealed class PostgresUniqueViolationTranslator(
    IEnumerable<IDbUpdateExceptionHandler> handlers)
    : IDbUpdateExceptionTranslator
{
    private readonly IReadOnlyDictionary<string, IDbUpdateExceptionHandler>
        _handlers = handlers.ToDictionary(
            handler => handler.ConstraintName,
            StringComparer.Ordinal);

    public Exception? Translate(DbUpdateException exception)
    {
        if (exception.InnerException is not PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation,
                ConstraintName: { } constraintName
            })
        {
            return null;
        }

        return _handlers.TryGetValue(constraintName, out var handler)
            ? handler.Handle(exception)
            : null;
    }
}
