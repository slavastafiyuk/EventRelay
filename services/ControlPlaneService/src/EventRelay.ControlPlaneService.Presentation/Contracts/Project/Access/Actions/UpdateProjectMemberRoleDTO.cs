using System.ComponentModel.DataAnnotations;

namespace EventRelay.ControlPlaneService.Presentation.Contracts.Project.Access.Actions;

public sealed record UpdateProjectMemberRoleDTO : ProjectMemberActionDTO
{
    [EnumDataType(typeof(ProjectRoleDTO))]
    public required ProjectRoleDTO Role { get; init; }
}
