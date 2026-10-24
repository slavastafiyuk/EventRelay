using EventRelay.ControlPlaneService.Infrastructure.Persistence.Exceptions.Abstraction;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace EventRelay.ControlPlaneService.Infrastructure.Persistence;

public sealed class DbUpdateExceptionPipeline
{
    private readonly DbUpdateExceptionTranslator _translator;
    private readonly IReadOnlyDictionary<string, IDbUpdateRecoveryStrategy> _recoveryStrategies;

    public DbUpdateExceptionPipeline(
        DbUpdateExceptionTranslator translator,
        IEnumerable<IDbUpdateRecoveryStrategy> recoveryStrategies)
    {
        _translator = translator;
        _recoveryStrategies = recoveryStrategies.ToDictionary(
            strategy => strategy.ConstraintName,
            StringComparer.Ordinal);
    }
    public Exception? Process(DbUpdateException exception)
    {
        var translated = _translator.Translate(exception);
        if (translated is null) return null;

        var constraintName = (exception.InnerException as PostgresException)?.ConstraintName;

        if (constraintName is null ||
            !_recoveryStrategies.TryGetValue(constraintName, out var recovery))
            return translated;

        if (!recovery.TryRecover(exception)) return null;


        return translated;
    }

}
