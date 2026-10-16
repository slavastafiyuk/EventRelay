using EventRelay.ProjectService.Application.Abstractions.Persistence;

namespace EventRelay.ProjectService.Infrastructure.Persistence;

public sealed class EfUnitOfWork(ProjectDbContext dbContext) : IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken) => dbContext.SaveChangesAsync(cancellationToken);
}
