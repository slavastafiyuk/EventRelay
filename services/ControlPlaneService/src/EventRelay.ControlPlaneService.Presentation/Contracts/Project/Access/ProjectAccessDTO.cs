namespace EventRelay.ControlPlaneService.Presentation.Contracts.Project.Access;

public sealed record ProjectAccessDTO
{
    public ProjectVisibilityDTO Visibility { get; init; } = ProjectVisibilityDTO.Private;
    public List<ProjectMemberDTO> Members { get; init; } = [];
}
