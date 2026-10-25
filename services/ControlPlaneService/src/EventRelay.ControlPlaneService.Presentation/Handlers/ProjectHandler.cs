using EventRelay.ControlPlaneService.Application.Abstractions.Projects;
using EventRelay.ControlPlaneService.Domain.Entities.Project;
using EventRelay.ControlPlaneService.Presentation.Abstractions;
using EventRelay.ControlPlaneService.Presentation.Contracts.Project;
using Mapster;
using MapsterMapper;

namespace EventRelay.ControlPlaneService.Presentation.Handlers;

public class ProjectHandler(
    IProjectService projectService,
    IMapper mapper) : IProjectHandler
{
    public async Task<ProjectKeyDTO> CreateAsync(ProjectCreateDTO dto, CancellationToken cancellationToken)
    {
        var project = mapper.Map<Project>(dto);

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
