using EventRelay.ControlPlaneService.Domain.Entities.Api;

namespace EventRelay.ControlPlaneService.Application.Abstractions.Api;

public interface IProjectApiService
{
    Task<IssuedApi> IssueAsync(ProjectApi api, CancellationToken ct);
    Task RevokeAsync(ApiKey key, CancellationToken ct);
}
