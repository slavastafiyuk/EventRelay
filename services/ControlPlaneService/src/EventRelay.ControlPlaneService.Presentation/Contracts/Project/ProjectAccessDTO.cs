using EventRelay.ControlPlaneService.Presentation.Contracts.User;

namespace EventRelay.ControlPlaneService.Presentation.Contracts.Project;

public sealed record ProjectAccessDTO
{
    public ProjectVisibilityDTO Visibility { get; init; } = ProjectVisibilityDTO.Private;
    public List<UserKeyDTO> Users { get; init; } = new();
}
