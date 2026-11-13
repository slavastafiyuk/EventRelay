using EventRelay.ControlPlaneService.Domain.Entities.Project;
using EventRelay.ControlPlaneService.Domain.Entities.Project.Access;
using EventRelay.ControlPlaneService.Domain.Entities.User;

namespace EventRelay.ControlPlaneService.Application.Abstractions.Projects;

public interface IProjectService
{
    Task<ProjectKey> CreateAsync(Project project, CancellationToken cancellationToken);
    Task<Project?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task AddMemberAsync(ProjectKey key, UserKey user, ProjectRole role, CancellationToken cancellationToken)
}
