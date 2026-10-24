using EventRelay.ControlPlaneService.Application.Abstractions.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EventRelay.ControlPlaneService.Infrastructure.Persistence;

public sealed class EfUnitOfWork(
    ControlPlaneDbContext dbContext,
    DbUpdateExceptionPipeline exceptionPipeline) : IUnitOfWork
{
    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            var translated = exceptionPipeline.Process(ex);

            if (translated is null) throw;

            throw translated;
        }
    }
}
