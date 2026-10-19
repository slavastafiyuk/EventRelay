using CommunityToolkit.Diagnostics;
using EventRelay.ControlPlaneService.Application.Abstractions.Persistence;
using EventRelay.ControlPlaneService.Application.Abstractions.Projects;
using EventRelay.ControlPlaneService.Domain.Entities.Project;

namespace EventRelay.ControlPlaneService.Application.Services;

public sealed class ProjectService(
    IProjectRepository repository,
    IUnitOfWork unitOfWork) : IProjectService
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

        return project;
    }
}
