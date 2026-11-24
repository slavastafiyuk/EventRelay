namespace EventRelay.ControlPlaneService.Application.Abstractions.Common;

public interface IExistingKeyService<TKey> where TKey : class
{
    Task<TKey> LoadKeyAsync(TKey key, CancellationToken cancellationToken);
}
