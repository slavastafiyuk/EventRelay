using EventRelay.ControlPlaneService.Presentation.Contracts.Organization;
using EventRelay.ControlPlaneService.Presentation.Contracts.Organization.Access.Actions;

namespace EventRelay.ControlPlaneService.Presentation.Abstractions;

public interface IOrganizationHandler
{
    Task<OrganizationKeyDTO> CreateAsync(OrganizationCreateDTO dto, CancellationToken cancellationToken);
    Task<OrganizationDTO?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task ManageMemberAsync(Guid organizationId, OrganizationMemberActionDTO dto, CancellationToken cancellationToken);
}
