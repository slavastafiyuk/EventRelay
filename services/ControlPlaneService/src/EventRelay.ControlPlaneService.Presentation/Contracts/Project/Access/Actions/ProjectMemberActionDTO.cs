using System.Text.Json.Serialization;
using EventRelay.ControlPlaneService.Presentation.Contracts.User;

namespace EventRelay.ControlPlaneService.Presentation.Contracts.Project.Access.Actions;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "action")]
[JsonDerivedType(typeof(AddProjectMemberDTO), "AddNewMember")]
[JsonDerivedType(typeof(UpdateProjectMemberRoleDTO), "UpdateRole")]
[JsonDerivedType(typeof(DeleteProjectMemberDTO), "Delete")]
public abstract record ProjectMemberActionDTO
{
    public required UserKeyDTO UserKey { get; init; }
}
