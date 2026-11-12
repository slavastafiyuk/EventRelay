using EventRelay.ControlPlaneService.Presentation.Contracts.User;

namespace EventRelay.ControlPlaneService.Presentation.Contracts.Organization.Access;

public sealed record OrganizationMemberDTO
{
    public required UserKeyDTO User { get; init; }
    public OrganizationRoleDTO Role { get; init; }
    public DateTimeOffset JoinedAt { get; init; }
}
