using EventRelay.ProjectService.Domain.Entities.Project;

namespace EventRelay.ProjectService.Application.Abstractions;

public interface IProjectRepository
{
    Task AddAsync(
    Project project,
    CancellationToken cancellationToken);

    Task<Project?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken);
}
