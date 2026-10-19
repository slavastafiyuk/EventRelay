using EventRelay.ControlPlaneService.Application.Models;
using EventRelay.ControlPlaneService.Domain.Entities.Project;
using EventRelay.ControlPlaneService.Presentation.Contracts.Ping;
using EventRelay.ControlPlaneService.Presentation.Contracts.Project;
using Mapster;

namespace EventRelay.ControlPlaneService.Presentation.Mappings;

public sealed class Mapping : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<ProjectStatus, PingResponseDTO>();
        config.NewConfig<ProjectKey, ProjectKeyDTO>();
        config.NewConfig<Project, ProjectDTO>()
            .Map(dest => dest.Key, src => src.Key);

        config.NewConfig<ProjectKeyDTO, ProjectKey>()
            .MapToConstructor(true);

        config.NewConfig<ProjectDTO, Project>()
            .MapToConstructor(true);

        config.NewConfig<ProjectCreateDTO, Project>()
            .ConstructUsing(src => new Project(new ProjectKey(src.Name)));
    }
}
