using System.ComponentModel.DataAnnotations;

namespace EventRelay.ControlPlaneService.Presentation.Contracts.Organization.Access.Actions;

public sealed record AddOrganizationMemberDTO : OrganizationMemberActionDTO
{
    [EnumDataType(typeof(OrganizationRoleDTO))]
    public required OrganizationRoleDTO Role { get; init; }
}
