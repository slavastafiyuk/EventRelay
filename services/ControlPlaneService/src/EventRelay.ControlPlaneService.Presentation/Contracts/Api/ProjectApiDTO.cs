using EventRelay.ControlPlaneService.Presentation.Contracts.Project;
using EventRelay.ControlPlaneService.Presentation.Contracts.User;

namespace EventRelay.ControlPlaneService.Presentation.Contracts.Api;

public sealed record ProjectApiDTO
{
    public required ApiKeyDTO Key { get; init; }
    public required ProjectKeyDTO Project { get; init; }
    public required UserKeyDTO CreatedBy { get; init; }
    public UserKeyDTO? RevokedBy { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? RevokedAt { get; init; }
    public required bool IsActive { get; init; }
}
