using EventRelay.ControlPlaneService.Presentation.Contracts.Project;
using EventRelay.ControlPlaneService.Presentation.Contracts.Project.Access;
using EventRelay.ControlPlaneService.Presentation.Contracts.Project.Access.Actions;

namespace EventRelay.ControlPlaneService.Presentation.Abstractions;

public interface IProjectHandler
{
    Task<ProjectKeyDTO> CreateAsync(ProjectCreateDTO dto, CancellationToken cancellationToken);
    Task<ProjectDTO?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task ManageMemberAsync(Guid id, ProjectMemberActionDTO dto, CancellationToken cancellationToken);
    Task ChangeVisibilityAsync(Guid id, ProjectVisibilityDTO visibility, CancellationToken cancellationToken);
}
