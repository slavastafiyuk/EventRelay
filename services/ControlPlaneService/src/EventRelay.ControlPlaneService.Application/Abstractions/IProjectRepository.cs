using EventRelay.ControlPlaneService.Domain.Entities.Project;

namespace EventRelay.ControlPlaneService.Application.Abstractions;

public interface IProjectRepository
{
    Task AddAsync(
    Project project,
    CancellationToken cancellationToken);

    Task<Project?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken);
}
