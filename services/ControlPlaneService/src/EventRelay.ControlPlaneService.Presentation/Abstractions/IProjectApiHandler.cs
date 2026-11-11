using EventRelay.ControlPlaneService.Presentation.Contracts.Api;

namespace EventRelay.ControlPlaneService.Presentation.Abstractions;

public interface IProjectApiHandler
{
    Task<IssuedApiDTO> IssueAsync(Guid projectId, CreateApiKeyDTO dto, CancellationToken ct);
    Task RevokeAsync(Guid projectId, Guid apiKeyId, CancellationToken ct);
    Task<ResolvedApiKeyDTO?> ResolveAsync(ResolveApiKeyDTO dto, CancellationToken cancellationToken);
}
