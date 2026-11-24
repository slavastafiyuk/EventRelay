using EventRelay.ControlPlaneService.Application.Abstractions.Common;
using EventRelay.ControlPlaneService.Domain.Entities.Project;
using EventRelay.ControlPlaneService.Domain.Entities.Project.Access;
using EventRelay.ControlPlaneService.Domain.Entities.User;

namespace EventRelay.ControlPlaneService.Application.Abstractions.Projects;

public interface IProjectService : IExistingKeyService<ProjectKey>
{
    Task<ProjectKey> CreateAsync(Project project, CancellationToken cancellationToken);
    Task<Project?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task AddMemberAsync(Guid id, UserKey user, ProjectRole role, CancellationToken cancellationToken);
    Task ChangeMemberRoleAsync(Guid id, UserKey user, ProjectRole role, CancellationToken cancellationToken);
    Task RemoveMemberAsync(Guid id, UserKey user, CancellationToken cancellationToken);
    Task ChangeVisibilityAsync(Guid id, ProjectVisibility visibility, CancellationToken cancellationToken);
}
