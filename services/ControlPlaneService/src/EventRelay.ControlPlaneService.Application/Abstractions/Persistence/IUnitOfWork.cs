using System.Data;

namespace EventRelay.ControlPlaneService.Application.Abstractions.Persistence;

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
    Task<TResult> ExecuteInTransactionAsync<TResult>(Func<CancellationToken, Task<TResult>> operation, IsolationLevel isolationLevel, CancellationToken cancellationToken);
}
