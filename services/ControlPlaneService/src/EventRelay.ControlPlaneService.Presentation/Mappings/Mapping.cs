using EventRelay.ControlPlaneService.Application.Models;
using EventRelay.ControlPlaneService.Domain.Entities.Project;
using EventRelay.ControlPlaneService.Domain.Entities.User;
using EventRelay.ControlPlaneService.Presentation.Contracts.Ping;
using EventRelay.ControlPlaneService.Presentation.Contracts.Project;
using EventRelay.ControlPlaneService.Presentation.Contracts.User;
using Mapster;

namespace EventRelay.ControlPlaneService.Presentation.Mappings;

public sealed class Mapping : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<ProjectStatus, PingResponseDTO>();

        #region Project Mappings

        config.NewConfig<ProjectKey, ProjectKeyDTO>();
        config.NewConfig<Project, ProjectDTO>()
            .Map(dest => dest.Key, src => src.Key);
        config.NewConfig<ProjectKeyDTO, ProjectKey>()
            .MapToConstructor(true);
        config.NewConfig<ProjectDTO, Project>()
            .MapToConstructor(true);
        config.NewConfig<ProjectCreateDTO, Project>()
            .ConstructUsing(src => new Project(new ProjectKey(src.Name)));

        #endregion Project Mappings

        #region User Mappings

        config.NewConfig<UserCreateDTO, User>()
            .MapWith(src => new User(new UserKey(src.Name),
            src.Issuer,
            src.Subject));

        #endregion User Mappings
    }
}
