using EventRelay.ControlPlaneService.Application.Abstractions.Api;
using EventRelay.ControlPlaneService.Presentation.Abstractions;
using EventRelay.ControlPlaneService.Presentation.Contracts.Api;
using Mapster;

namespace EventRelay.ControlPlaneService.Presentation.Handlers;

public sealed class ProjectApiHandler(
    IProjectApiService service) : IProjectApiHandler
{
    public async Task<IssuedApiDTO> IssueAsync(Guid projectId, CreateApiKeyDTO dto, CancellationToken ct)
    {
        var issued = await service.IssueAsync(projectId, dto.Name, ct);

        return issued.Adapt<IssuedApiDTO>();
    }

    public Task RevokeAsync(Guid projectId, Guid apiKeyId, CancellationToken ct)
    {
        return service.RevokeAsync(projectId, apiKeyId, ct);
    }

    public async Task<ResolvedApiKeyDTO?> ResolveAsync(ResolveApiKeyDTO dto, CancellationToken cancellationToken)
    {
        var projectId = await service.ResolveProjectIdAsync(dto.KeyHash.ToUpperInvariant(), cancellationToken);

        if (projectId is null) return null;

        return new ResolvedApiKeyDTO { ProjectId = projectId.Value };
    }
}
