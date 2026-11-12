using System.Text.Json.Serialization;
using EventRelay.ControlPlaneService.Presentation.Contracts.User;

namespace EventRelay.ControlPlaneService.Presentation.Contracts.Organization.Access.Actions;


[JsonPolymorphic(TypeDiscriminatorPropertyName = "action")]
[JsonDerivedType(typeof(DeleteOrganizationMemberDTO), nameof(OrganizationMemberActionTypeDTO.Delete))]
[JsonDerivedType(typeof(UpdateOrganizationMemberToAdminDTO), nameof(OrganizationMemberActionTypeDTO.SetAdmin))]
[JsonDerivedType(typeof(UpdateOrganizationMemberToMemberDTO), nameof(OrganizationMemberActionTypeDTO.SetMember))]
[JsonDerivedType(typeof(AddOrganizationMemberDTO), nameof(OrganizationMemberActionTypeDTO.AddNewMember))]
public abstract record OrganizationMemberActionDTO
{
    public required UserKeyDTO UserKey { get; init; }
}
