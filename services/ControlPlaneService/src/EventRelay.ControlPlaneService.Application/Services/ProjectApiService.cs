using EventRelay.ControlPlaneService.Application.Abstractions.Api;
using EventRelay.ControlPlaneService.Domain.Entities.Api;

namespace EventRelay.ControlPlaneService.Application.Services;

public class ProjectApiService : IProjectApiService
{
    public Task<IssuedApi> IssueAsync(ProjectApi api, CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public Task RevokeAsync(ApiKey key, CancellationToken ct)
    {
        throw new NotImplementedException();
    }
}
