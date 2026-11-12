
using EventRelay.ControlPlaneService.Infrastructure.Persistence.Exceptions.Abstraction;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace EventRelay.ControlPlaneService.Infrastructure.Persistence;

public sealed class DbUpdateExceptionPipeline
{
    private readonly IReadOnlyList<IDbUpdateExceptionTranslator> _translators;
    private readonly IReadOnlyDictionary<string, IDbUpdateRecoveryStrategy> _recoveryStrategies;

    public DbUpdateExceptionPipeline(
        IEnumerable<IDbUpdateExceptionTranslator> translators,
        IEnumerable<IDbUpdateRecoveryStrategy> recoveryStrategies)
    {
        _translators = translators.ToArray();

        _recoveryStrategies = recoveryStrategies.ToDictionary(
            strategy => strategy.ConstraintName,
            StringComparer.Ordinal);
    }

    public Exception? Process(DbUpdateException exception)
    {
        Exception? translated = null;

        foreach (var translator in _translators)
        {
            translated = translator.Translate(exception);

            if (translated is not null)
                break;
        }

        if (translated is null)
            return null;

        if (exception.InnerException is not PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation,
                ConstraintName: { } constraintName
            })
        {
            return translated;
        }

        if (!_recoveryStrategies.TryGetValue(
                constraintName, out var recovery))
        {
            return translated;
        }

        if (!recovery.TryRecover(exception))
            return null;

        return translated;
    }
}
