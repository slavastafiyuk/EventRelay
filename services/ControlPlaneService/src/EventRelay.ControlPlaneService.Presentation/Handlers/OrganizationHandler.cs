
using EventRelay.ControlPlaneService.Application.Abstractions.Authentication;
using EventRelay.ControlPlaneService.Application.Abstractions.Organizations;
using EventRelay.ControlPlaneService.Domain.Entities.Organization;
using EventRelay.ControlPlaneService.Domain.Entities.Organization.Access;
using EventRelay.ControlPlaneService.Presentation.Abstractions;
using EventRelay.ControlPlaneService.Presentation.Contracts.Organization;
using EventRelay.ControlPlaneService.Presentation.Contracts.Organization.Access.Actions;
using Mapster;

namespace EventRelay.ControlPlaneService.Presentation.Handlers;

public sealed class OrganizationHandler(
    IOrganizationService organizationService,
    ICurrentUser currentUser) : IOrganizationHandler
{
    public async Task<OrganizationKeyDTO> CreateAsync(OrganizationCreateDTO dto, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var owner = currentUser.UserKey
            ?? throw new InvalidOperationException("Current user is not initialized.");

        var organization = new Organization(
            dto.Adapt<OrganizationKey>(),
            owner);

        var result = await organizationService.CreateAsync(
            organization,
            cancellationToken);

        return result.Adapt<OrganizationKeyDTO>();
    }

    public async Task<OrganizationDTO?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var result = await organizationService.GetByIdAsync(
            id,
            cancellationToken);

        return result?.Adapt<OrganizationDTO>();
    }


    public Task ManageMemberAsync(
        Guid organizationId,
        OrganizationMemberActionDTO dto,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dto);
        ArgumentNullException.ThrowIfNull(dto.UserKey);

        var userId = dto.UserKey.Id;

        return dto switch
        {
            AddOrganizationMemberDTO add =>
                organizationService.AddMemberAsync(
                    organizationId,
                    userId,
                    (OrganizationRole)add.Role,
                    cancellationToken),

            UpdateOrganizationMemberToAdminDTO =>
                organizationService.ChangeMemberRoleAsync(
                    organizationId,
                    userId,
                    OrganizationRole.Admin,
                    cancellationToken),

            UpdateOrganizationMemberToMemberDTO =>
                organizationService.ChangeMemberRoleAsync(
                    organizationId,
                    userId,
                    OrganizationRole.Member,
                    cancellationToken),

            DeleteOrganizationMemberDTO =>
                organizationService.RemoveMemberAsync(
                    organizationId,
                    userId,
                    cancellationToken),

            _ => throw new ArgumentOutOfRangeException(nameof(dto), "Unsupported organization member action.")
        };
    }

}
