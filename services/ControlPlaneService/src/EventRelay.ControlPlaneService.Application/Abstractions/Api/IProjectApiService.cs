using EventRelay.ControlPlaneService.Domain.Entities.Api;

namespace EventRelay.ControlPlaneService.Application.Abstractions.Api;

public interface IProjectApiService
{
    Task<IssuedApi> IssueAsync(Guid projectId, string name, CancellationToken ct);
    Task RevokeAsync(Guid projectId, Guid apiKeyId, CancellationToken ct);
    Task<Guid?> ResolveProjectIdAsync(string keyHash, CancellationToken cancellationToken);
}
