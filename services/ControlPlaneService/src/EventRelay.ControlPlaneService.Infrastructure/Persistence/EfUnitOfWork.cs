using EventRelay.ControlPlaneService.Application.Abstractions.Persistence;

namespace EventRelay.ControlPlaneService.Infrastructure.Persistence;

public sealed class EfUnitOfWork(ProjectDbContext dbContext) : IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken) => dbContext.SaveChangesAsync(cancellationToken);
}
