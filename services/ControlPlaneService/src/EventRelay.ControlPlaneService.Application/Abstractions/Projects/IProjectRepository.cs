using EventRelay.ControlPlaneService.Application.Abstractions.Common;
using EventRelay.ControlPlaneService.Domain.Entities.Project;
using EventRelay.ControlPlaneService.Domain.Entities.Project.Access;
using EventRelay.ControlPlaneService.Domain.Entities.User;

namespace EventRelay.ControlPlaneService.Application.Abstractions.Projects;

public interface IProjectRepository : IExistingKeyRepository<ProjectKey>
{
    Task AddAsync(Project project, CancellationToken cancellationToken);
    Task<Project?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task AddMemberAsync(ProjectKey key, ProjectMember member, CancellationToken cancellationToken);
    Task ChangeMemberRoleAsync(ProjectKey key, ProjectMember member, CancellationToken cancellationToken);
    Task RemoveMemberAsync(ProjectKey key, UserKey user, CancellationToken cancellationToken);
    Task ChangeVisibilityAsync(ProjectKey key, ProjectVisibility visibility, CancellationToken cancellationToken);
}
