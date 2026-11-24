using EventRelay.ControlPlaneService.Application.Abstractions.Common;
using Mapster;

namespace EventRelay.ControlPlaneService.Presentation.Helpers;

public sealed class KeyResolver<TKey, TService>(TService service)
    where TKey : class
    where TService : class, IExistingKeyService<TKey>
{
    public Task<TKey> ResolveAsync(object dto, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dto);

        return service.LoadKeyAsync(dto.Adapt<TKey>(), cancellationToken);
    }
}
