using EventRelay.ControlPlaneService.Presentation.Contracts.Organization.Access;
using EventRelay.ControlPlaneService.Presentation.Contracts.User;

namespace EventRelay.ControlPlaneService.Presentation.Contracts.Organization;

public sealed record OrganizationDTO
{
    public required OrganizationKeyDTO Key { get; init; }
    public required UserKeyDTO Owner { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public IReadOnlyList<OrganizationMemberDTO> Members { get; init; } = [];
}
