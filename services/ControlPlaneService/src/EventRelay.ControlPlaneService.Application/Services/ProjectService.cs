using CommunityToolkit.Diagnostics;
using EventRelay.ControlPlaneService.Application.Abstractions.Authentication;
using EventRelay.ControlPlaneService.Application.Abstractions.Organizations;
using EventRelay.ControlPlaneService.Application.Abstractions.Persistence;
using EventRelay.ControlPlaneService.Application.Abstractions.Projects;
using EventRelay.ControlPlaneService.Application.Abstractions.Users;
using EventRelay.ControlPlaneService.Application.Exceptions;
using EventRelay.ControlPlaneService.Domain.Entities.Project;
using EventRelay.ControlPlaneService.Domain.Entities.Project.Access;
using EventRelay.ControlPlaneService.Domain.Entities.User;

namespace EventRelay.ControlPlaneService.Application.Services;

public sealed class ProjectService(
    IProjectRepository repository,
    IOrganizationService organizationService,
    IUnitOfWork unitOfWork,
    IUserService userService,
    ICurrentUser currentUser) : IProjectService
{
    #region Project Management

    public async Task<ProjectKey> CreateAsync(
        Project project,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(project.Organization);

        Guard.IsNotNullOrWhiteSpace(project.Key.Name);

        if (!project.Owner.Equals(currentUser.UserKey))
        {
            throw new UnauthorizedAccessException(
                "Project owner must match the current user.");
        }

        var organizationKey =
            await organizationService.LoadKeyAsync(
                project.Organization,
                cancellationToken);

        if (!await organizationService.IsMemberAsync(
                organizationKey,
                currentUser.UserKey,
                cancellationToken))
        {
            throw new KeyNotFoundException(
                "Organization not found.");
        }

        if (project.Access.Members.Count > 0)
        {
            throw new ArgumentException(
                "Project members cannot be assigned during creation.");
        }

        project.RefreshOrganizationKey(organizationKey);

        await repository.AddAsync(
            project,
            cancellationToken);

        await unitOfWork.SaveChangesAsync(
            cancellationToken);

        return project.Key;
    }

    public Task<Project?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        return GetAuthorizedAsync(
            id,
            ProjectPermission.Read,
            cancellationToken);
    }

    public async Task<Project?> GetAuthorizedAsync(
        Guid id,
        ProjectPermission permission,
        CancellationToken cancellationToken)
    {
        var user = currentUser.UserKey
            ?? throw new InvalidOperationException(
                "Current user is not initialized.");

        var project = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (project is null)
            return null;

        if (project.Organization is not { } organization)
            return null;

        if (!project.HasPermission(user, permission))
            throw new ProjectAccessDeniedException();

        var isPublicRead =
            permission == ProjectPermission.Read &&
            project.Access.Visibility == ProjectVisibility.Public;

        if (isPublicRead)
            return project;

        var isOrganizationMember =
            await organizationService.IsMemberAsync(
                organization,
                user,
                cancellationToken);

        if (!isOrganizationMember)
            return null;

        return project;
    }

    public async Task<ProjectKey> LoadKeyAsync(
        ProjectKey key,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);

        return await repository.GetKeyAsync(
                   key,
                   cancellationToken)
               ?? throw new KeyNotFoundException(
                   "Project not found.");
    }

    #endregion Project Management

    #region Members Management

    public async Task AddMemberAsync(
        Guid id,
        UserKey user,
        ProjectRole role,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);

        var project = await GetAuthorizedAsync(
                          id,
                          ProjectPermission.ManageMembers,
                          cancellationToken)
                      ?? throw new KeyNotFoundException(
                          "Project not found.");

        var validatedUser =
            await GetOrganizationMemberAsync(
                project,
                user,
                cancellationToken);

        var member = project.AddMember(
            validatedUser,
            role);

        await repository.AddMemberAsync(
            project.Key,
            member,
            cancellationToken);

        await unitOfWork.SaveChangesAsync(
            cancellationToken);
    }

    public async Task ChangeMemberRoleAsync(
        Guid id,
        UserKey user,
        ProjectRole role,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);

        var project = await GetAuthorizedAsync(
                          id,
                          ProjectPermission.ManageMembers,
                          cancellationToken)
                      ?? throw new KeyNotFoundException(
                          "Project not found.");

        var validatedUser =
            await GetOrganizationMemberAsync(
                project,
                user,
                cancellationToken);

        var member = project.ChangeMemberRole(
            validatedUser,
            role);

        await repository.ChangeMemberRoleAsync(
            project.Key,
            member,
            cancellationToken);

        await unitOfWork.SaveChangesAsync(
            cancellationToken);
    }

    public async Task RemoveMemberAsync(
        Guid id,
        UserKey user,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);

        var project = await GetAuthorizedAsync(
                          id,
                          ProjectPermission.ManageMembers,
                          cancellationToken)
                      ?? throw new KeyNotFoundException(
                          "Project not found.");

        var validatedUser =
            await userService.LoadKeyAsync(
                user,
                cancellationToken);

        project.RemoveMember(validatedUser);

        await repository.RemoveMemberAsync(
            project.Key,
            validatedUser,
            cancellationToken);

        await unitOfWork.SaveChangesAsync(
            cancellationToken);
    }

    public async Task ChangeVisibilityAsync(
        Guid id,
        ProjectVisibility visibility,
        CancellationToken cancellationToken)
    {
        var project = await GetAuthorizedAsync(
                          id,
                          ProjectPermission.ManageMembers,
                          cancellationToken)
                      ?? throw new KeyNotFoundException(
                          "Project not found.");

        project.ChangeVisibility(visibility);

        await repository.ChangeVisibilityAsync(
            project.Key,
            project.Access.Visibility,
            cancellationToken);

        await unitOfWork.SaveChangesAsync(
            cancellationToken);
    }

    #endregion Members Management

    #region Helper Methods

    private async Task<UserKey> GetOrganizationMemberAsync(
        Project project,
        UserKey user,
        CancellationToken cancellationToken)
    {
        var organization = project.Organization
            ?? throw new InvalidOperationException(
                "Project has no organization.");

        var validatedUser =
            await userService.LoadKeyAsync(
                user,
                cancellationToken);

        var isMember =
            await organizationService.IsMemberAsync(
                organization,
                validatedUser,
                cancellationToken);

        if (!isMember)
        {
            throw new KeyNotFoundException(
                "User not found.");
        }

        return validatedUser;
    }

    #endregion Helper Methods
}
