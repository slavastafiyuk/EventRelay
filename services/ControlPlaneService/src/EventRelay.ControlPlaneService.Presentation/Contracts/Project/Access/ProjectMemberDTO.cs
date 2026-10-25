using EventRelay.ControlPlaneService.Presentation.Contracts.User;

namespace EventRelay.ControlPlaneService.Presentation.Contracts.Project.Access;

public sealed record ProjectMemberDTO
{
    public required UserKeyDTO User { get; init; }
    public required ProjectRoleDTO Role { get; init; }
}
