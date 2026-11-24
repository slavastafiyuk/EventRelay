namespace EventRelay.ControlPlaneService.Application.Abstractions.Common;

public interface IExistingKeyRepository<TKey> where TKey : class
{
    Task<TKey> GetKeyAsync(TKey key, CancellationToken cancellationToken);
}
