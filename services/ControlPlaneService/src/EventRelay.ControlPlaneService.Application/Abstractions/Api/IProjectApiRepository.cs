using EventRelay.ControlPlaneService.Domain.Entities.Api;

namespace EventRelay.ControlPlaneService.Application.Abstractions.Api;

public interface IProjectApiRepository
{
    Task AddAsync(ProjectApi api, CancellationToken ct);
    Task<ProjectApi?> GetByIdAsync(Guid id, CancellationToken ct);
    Task MarkRevokedAsync(ProjectApi api, CancellationToken ct);
    Task<Guid?> ResolveProjectIdAsync(string keyHash, CancellationToken ct);
}
