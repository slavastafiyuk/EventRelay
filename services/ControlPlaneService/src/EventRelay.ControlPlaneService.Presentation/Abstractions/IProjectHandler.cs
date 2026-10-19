using EventRelay.ControlPlaneService.Presentation.Contracts.Project;

namespace EventRelay.ControlPlaneService.Presentation.Abstractions;

public interface IProjectHandler
{
    Task<ProjectKeyDTO> CreateAsync(
        ProjectCreateDTO dto,
        CancellationToken cancellationToken);

    Task<ProjectDTO?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken);
}
