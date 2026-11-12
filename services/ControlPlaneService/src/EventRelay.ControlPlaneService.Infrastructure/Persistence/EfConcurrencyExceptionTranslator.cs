
using EventRelay.ControlPlaneService.Infrastructure.Persistence.Exceptions.Abstraction;
using Microsoft.EntityFrameworkCore;

namespace EventRelay.ControlPlaneService.Infrastructure.Persistence;

public sealed class EfConcurrencyExceptionTranslator(
    IEnumerable<IDbUpdateConcurrencyHandler> handlers)
    : IDbUpdateExceptionTranslator
{
    private readonly IReadOnlyList<IDbUpdateConcurrencyHandler>
        _handlers = handlers.ToArray();

    public Exception? Translate(DbUpdateException exception)
    {
        if (exception is not DbUpdateConcurrencyException
            concurrencyException)
        {
            return null;
        }

        var handler = _handlers.FirstOrDefault(
            h => h.CanHandle(concurrencyException));

        return handler?.Handle(concurrencyException);
    }
}
