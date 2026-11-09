using EventRelay.ControlPlaneService.Application.Abstractions.Api;
using EventRelay.ControlPlaneService.Domain.Entities.Api;
using EventRelay.ControlPlaneService.Infrastructure.Persistence;
using EventRelay.ControlPlaneService.Infrastructure.Persistence.Models.Api;
using Mapster;
using Microsoft.EntityFrameworkCore;

namespace EventRelay.ControlPlaneService.Repository.Repositories;


public sealed class ProjectApiRepository(ControlPlaneDbContext db) : IProjectApiRepository
{
    public async Task AddAsync(ProjectApi api, CancellationToken ct)
    {
        await db.ProjectApiKeys.AddAsync(api.Adapt<ProjectApiRecord>(), ct);
    }

    public async Task<ProjectApi?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var record = await db.ProjectApiKeys
            .AsNoTracking()
            .Include(x => x.Project)
            .Include(x => x.CreatedBy)
            .Include(x => x.RevokedBy)
            .SingleOrDefaultAsync(x => x.Id == id, ct);

        return record?.Adapt<ProjectApi>();
    }

    public async Task MarkRevokedAsync(ProjectApi api, CancellationToken ct)
    {
        var record = await db.ProjectApiKeys.SingleAsync(x => x.Id == api.Key.Id, ct);
        record.RevokedByUserId = api.RevokedBy?.Id;
        record.RevokedAt = api.RevokedAt;
    }

    public Task<Guid?> ResolveProjectIdAsync(string keyHash, CancellationToken ct)
    {
        return db.ProjectApiKeys
            .AsNoTracking()
            .Where(x => x.KeyHash == keyHash && x.RevokedAt == null)
            .Select(x => (Guid?)x.ProjectId)
            .SingleOrDefaultAsync(ct);
    }
}

