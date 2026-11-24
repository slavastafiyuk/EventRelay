using EventRelay.ControlPlaneService.Application.Abstractions.Common;
using EventRelay.ControlPlaneService.Domain.Entities.Project;

namespace EventRelay.ControlPlaneService.Application.Abstractions.Projects;

public interface IProjectRepository : IExistingKeyRepository<ProjectKey>
{
    Task AddAsync(Project project, CancellationToken cancellationToken);
    Task<Project?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
}
