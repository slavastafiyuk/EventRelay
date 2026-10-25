using EventRelay.ControlPlaneService.Presentation.Contracts.User;

namespace EventRelay.ControlPlaneService.Presentation.Contracts.Project;

public sealed record ProjectDTO
{
    public required ProjectKeyDTO Key { get; init; }
    public string? Description { get; init; }
    public ProjectAccessDTO Access { get; init; } = new();
    public required UserKeyDTO Owner { get; init; }
}
