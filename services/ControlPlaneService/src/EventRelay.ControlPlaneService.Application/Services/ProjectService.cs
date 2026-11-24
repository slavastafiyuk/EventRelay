using CommunityToolkit.Diagnostics;
using EventRelay.ControlPlaneService.Application.Abstractions.Authentication;
using EventRelay.ControlPlaneService.Application.Abstractions.Organizations;
using EventRelay.ControlPlaneService.Application.Abstractions.Persistence;
using EventRelay.ControlPlaneService.Application.Abstractions.Projects;
using EventRelay.ControlPlaneService.Application.Abstractions.Users;
using EventRelay.ControlPlaneService.Application.Exceptions;
using EventRelay.ControlPlaneService.Domain.Entities.Project;
using EventRelay.ControlPlaneService.Domain.Entities.Project.Access;

namespace EventRelay.ControlPlaneService.Application.Services;

public sealed class ProjectService(
    IProjectRepository repository,
    IOrganizationService organizationService,
    IUnitOfWork unitOfWork,
    IUserService userService,
    ICurrentUser currentUser) : IProjectService
{

    public async Task<ProjectKey> CreateAsync(Project project, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(project.Organization);
        Guard.IsNotNullOrWhiteSpace(project.Key.Name);

        var actor = await userService.LoadKeyAsync(project.Owner, cancellationToken);

        if (!project.Owner.Equals(actor))
            throw new UnauthorizedAccessException("Project owner must match the current user.");


        var organizationKey = await organizationService.LoadKeyAsync(project.Organization, cancellationToken);

        if (!await organizationService.IsMemberAsync(organizationKey, actor, cancellationToken))
            throw new KeyNotFoundException("Organization not found.");

        if (project.Access.Members.Count > 0)
            throw new ArgumentException("Project members cannot be assigned during creation.");

        await repository.AddAsync(project, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return project.Key;
    }


    public Task<Project?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return GetAuthorizedAsync(id, ProjectPermission.Read, cancellationToken);
    }

    public async Task<ProjectKey> LoadKeyAsync(ProjectKey key, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);

        return await repository.GetKeyAsync(key, cancellationToken)
            ?? throw new KeyNotFoundException("Project not found.");
    }

    #region Members Management



    #endregion Memebrs Management

    #region Helper Methods

    private async Task<Project?> GetAuthorizedAsync(Guid projectId, ProjectPermission permission, CancellationToken cancellationToken)
    {
        var user = currentUser.UserKey
            ?? throw new InvalidOperationException("Current user is not initialized.");

        var project = await repository.GetByIdAsync(projectId, cancellationToken);

        if (project is null)
            return null;

        if (!project.HasPermission(user, permission))
            throw new ProjectAccessDeniedException();

        return project;
    }

    #endregion Helper Methods
}
