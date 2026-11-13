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

    public async Task<ProjectKey> CreateAsync(Project project, CancellationToken cancellationToken)
    {
        Guard.IsNotNullOrWhiteSpace(project.Key.Name);
        Guard.IsNotNull(project.Key.Id);

        await repository.AddAsync(project, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return project.Key;
    }

    public Task<Project?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return GetAuthorizedAsync(id, ProjectPermission.Read, cancellationToken);
    }

    #region Members Management

    public async Task AddMemberAsync(ProjectKey key, UserKey user, ProjectRole role, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

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
