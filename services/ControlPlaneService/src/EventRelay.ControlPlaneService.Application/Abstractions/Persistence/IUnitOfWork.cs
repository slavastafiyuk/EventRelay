namespace EventRelay.ControlPlaneService.Application.Abstractions.Persistence;

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(
        CancellationToken cancellationToken);
}
