
using EventRelay.ControlPlaneService.Application.Abstractions.Authentication;
using EventRelay.ControlPlaneService.Application.Abstractions.Organizations;
using EventRelay.ControlPlaneService.Application.Abstractions.Persistence;
using EventRelay.ControlPlaneService.Application.Abstractions.Users;
using EventRelay.ControlPlaneService.Domain.Entities.Organization;
using EventRelay.ControlPlaneService.Domain.Entities.Organization.Access;
using EventRelay.ControlPlaneService.Domain.Entities.User;

namespace EventRelay.ControlPlaneService.Application.Services;

public sealed class OrganizationService(
    IOrganizationRepository repository,
    IUnitOfWork unitOfWork,
    IUserService userService,
    ICurrentUser currentUser) : IOrganizationService
{
    public async Task<OrganizationKey> CreateAsync(Organization organization, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(organization);

        var user = currentUser.UserKey
            ?? throw new InvalidOperationException("Current user is not initialized.");

        if (!organization.IsOwner(user))
            throw new InvalidOperationException("Organization owner must match the current user.");

        await repository.AddAsync(organization, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return organization.Key;
    }

    public async Task<Organization?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var user = currentUser.UserKey
            ?? throw new InvalidOperationException("Current user is not initialized.");

        var organization = await repository.GetByIdAsync(id, cancellationToken);

        if (organization is null)
            return null;

        if (!organization.IsMember(user))
            return null;

        return organization;
    }


    public async Task AddMemberAsync(
        Guid organizationId,
        Guid userId,
        OrganizationRole role,
        CancellationToken cancellationToken)
    {
        var organization = await GetManageableOrganizationAsync(
            organizationId, cancellationToken);

        var user = await GetExistingUserAsync(
            userId, cancellationToken);

        if (organization.IsMember(user))
            throw new InvalidOperationException(
                "User is already a member of the organization.");

        organization.AddMember(user, role);

        var member = organization.Members.Single(
            m => m.User.Equals(user));

        await repository.AddMemberAsync(
            organizationId, member, cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task ChangeMemberRoleAsync(
        Guid organizationId,
        Guid userId,
        OrganizationRole role,
        CancellationToken cancellationToken)
    {
        var organization = await GetManageableOrganizationAsync(
            organizationId, cancellationToken);

        var user = await GetExistingUserAsync(
            userId, cancellationToken);

        if (!organization.IsMember(user))
            throw new KeyNotFoundException(
                "User is not a member of the organization.");

        organization.ChangeMemberRole(user, role);

        await repository.ChangeMemberRoleAsync(
            organizationId, user.Id, role, cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveMemberAsync(
        Guid organizationId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var organization = await GetManageableOrganizationAsync(
            organizationId, cancellationToken);

        var user = await GetExistingUserAsync(
            userId, cancellationToken);

        if (!organization.IsMember(user))
            throw new KeyNotFoundException(
                "User is not a member of the organization.");

        organization.RemoveMember(user);

        await repository.RemoveMemberAsync(
            organizationId, user.Id, cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    #region Helper Methods


    private async Task<Organization> GetManageableOrganizationAsync(
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        var organization = await GetByIdAsync(
            organizationId, cancellationToken)
            ?? throw new KeyNotFoundException(
                "Organization not found.");

        if (!organization.IsOwner(currentUser.UserKey))
            throw new UnauthorizedAccessException(
                "Only the organization owner can manage members.");

        return organization;
    }

    private async Task<UserKey> GetExistingUserAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException(
                "Invalid user ID.", nameof(userId));

        var user = await userService.GetByIdAsync(
            userId, cancellationToken);

        return user ?? throw new KeyNotFoundException(
            "User not found.");
    }


    #endregion Helper Methods
}
