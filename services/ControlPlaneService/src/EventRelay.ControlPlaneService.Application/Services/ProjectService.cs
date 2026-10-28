using CommunityToolkit.Diagnostics;
using EventRelay.ControlPlaneService.Application.Abstractions.Authentication;
using EventRelay.ControlPlaneService.Application.Abstractions.Persistence;
using EventRelay.ControlPlaneService.Application.Abstractions.Projects;
using EventRelay.ControlPlaneService.Application.Exceptions;
using EventRelay.ControlPlaneService.Domain.Entities.Project;

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

    public async Task<Project?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var project = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (project is null)
            return null;

        var user = currentUser.UserKey
            ?? throw new InvalidOperationException("Current user is not initialized.");

        if (!project.CanRead(user))
            throw new ProjectAccessDeniedException();

        return project;
    }
}
