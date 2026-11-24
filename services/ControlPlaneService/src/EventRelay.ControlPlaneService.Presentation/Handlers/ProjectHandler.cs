using EventRelay.ControlPlaneService.Application.Abstractions.Authentication;
using EventRelay.ControlPlaneService.Application.Abstractions.Projects;
using EventRelay.ControlPlaneService.Domain.Entities.Organization;
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
        ArgumentNullException.ThrowIfNull(dto);

        var project = dto.Adapt(
            new Project(
                new ProjectKey(dto.Name),
                currentUser.UserKey,
                dto.Organization.Adapt<OrganizationKey>()));
        var result = await projectService.CreateAsync(project, cancellationToken);

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
