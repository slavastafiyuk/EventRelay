using CommunityToolkit.Diagnostics;
using EventRelay.ControlPlaneService.Application.Abstractions.Authentication;
using EventRelay.ControlPlaneService.Application.Abstractions.Persistence;
using EventRelay.ControlPlaneService.Application.Abstractions.Projects;
using EventRelay.ControlPlaneService.Application.Exceptions;
using EventRelay.ControlPlaneService.Domain.Entities.Project;
using EventRelay.ControlPlaneService.Domain.Entities.Project.Access;

namespace EventRelay.ControlPlaneService.Application.Services;

public sealed class ProjectService(
    IProjectRepository repository,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser) : IProjectService
{
    public async Task<ProjectKey> CreateAsync(Project project, CancellationToken cancellationToken)
    {
        Guard.IsNotNullOrWhiteSpace(project.Key.Name);
        Guard.IsNotNull(project.Key.Id);

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

    private async Task<Project?> GetAuthorizedAsync(
        Guid projectId,
        ProjectPermission permission,
        CancellationToken cancellationToken)
    {
        var user = currentUser.UserKey
            ?? throw new InvalidOperationException("Current user is not initialized.");

        var project = await repository.GetByIdAsync(
            projectId,
            cancellationToken);

        if (project is null)
            return null;

        if (!project.HasPermission(user, permission))
            throw new ProjectAccessDeniedException();

        return project;
    }
}
