using EventRelay.ControlPlaneService.Domain.Entities.Project;

namespace EventRelay.ControlPlaneService.Application.Abstractions.Projects;

public interface IProjectService
{
    Task<ProjectKey> CreateAsync(
        Project project,
        CancellationToken cancellationToken);

    Task<Project?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken);
}
