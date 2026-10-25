using EventRelay.ControlPlaneService.Application.Abstractions.Authentication;
using EventRelay.ControlPlaneService.Application.Abstractions.Projects;
using EventRelay.ControlPlaneService.Domain.Entities.Project;
using EventRelay.ControlPlaneService.Presentation.Abstractions;
using EventRelay.ControlPlaneService.Presentation.Contracts.Project;
using Mapster;

namespace EventRelay.ControlPlaneService.Presentation.Handlers;

public class ProjectHandler(
    IProjectService projectService,
    ICurrentUser currentUser) : IProjectHandler
{
    public async Task<ProjectKeyDTO> CreateAsync(ProjectCreateDTO dto, CancellationToken cancellationToken)
    {
        var owner = currentUser.UserKey
            ?? throw new InvalidOperationException("Current user is not initialized.");

        var project = dto.Adapt(new Project(new ProjectKey(dto.Name), currentUser.UserKey));

        var result = await projectService.CreateAsync(
            project,
            cancellationToken);

        return result.Adapt<ProjectKeyDTO>();
    }

    public async Task<ProjectDTO?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var result = await projectService.GetByIdAsync(
           id,
           cancellationToken);

        return result?.Adapt<ProjectDTO>();
    }
}
