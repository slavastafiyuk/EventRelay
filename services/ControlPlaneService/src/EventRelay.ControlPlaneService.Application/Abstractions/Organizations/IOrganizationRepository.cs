using EventRelay.ControlPlaneService.Domain.Entities.Organization;
using EventRelay.ControlPlaneService.Domain.Entities.Organization.Access;

namespace EventRelay.ControlPlaneService.Application.Abstractions.Organizations;

public interface IOrganizationRepository
{
    Task AddAsync(Organization organization, CancellationToken cancellationToken);
    Task<Organization?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task AddMemberAsync(Guid organizationId, OrganizationMember member, CancellationToken cancellationToken);
    Task ChangeMemberRoleAsync(Guid organizationId, Guid userId, OrganizationRole role, CancellationToken cancellationToken);
    Task RemoveMemberAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken);
}
