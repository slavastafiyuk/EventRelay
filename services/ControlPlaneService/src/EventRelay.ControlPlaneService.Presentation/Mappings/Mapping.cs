using EventRelay.ControlPlaneService.Application.Models;
using EventRelay.ControlPlaneService.Domain.Entities.Project;
using EventRelay.ControlPlaneService.Domain.Entities.Project.Access;
using EventRelay.ControlPlaneService.Domain.Entities.User;
using EventRelay.ControlPlaneService.Presentation.Contracts.Ping;
using EventRelay.ControlPlaneService.Presentation.Contracts.Project;
using EventRelay.ControlPlaneService.Presentation.Contracts.Project.Access;
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
            .Map(dest => dest.Key, src => src.Key)
            .Map(dest => dest.Access, src => src.Access);
        config.NewConfig<ProjectKeyDTO, ProjectKey>()
            .MapToConstructor(true);
        config.NewConfig<ProjectDTO, Project>()
            .MapToConstructor(true);
        config.NewConfig<ProjectCreateDTO, Project>()
            .ConstructUsing(src => new Project(new ProjectKey(src.Name)));

        #endregion Project Mappings

        #region Project Access Mappings

        config.NewConfig<ProjectAccess, ProjectAccessDTO>()
            .Map(dest => dest.Visibility, src => (ProjectVisibilityDTO)src.Visibility);

        config.NewConfig<ProjectMember, ProjectMemberDTO>()
            .Map(dest => dest.Role, src => (ProjectRoleDTO)(int)src.Role);

        config.NewConfig<ProjectMemberDTO, ProjectMember>()
            .MapToConstructor(true)
            .Map(dest => dest.Role, src => (ProjectRole)src.Role);

        config.NewConfig<ProjectAccessDTO, ProjectAccess>()
            .MapToConstructor(true)
            .Map(dest => dest.Visibility, src => (ProjectVisibility)src.Visibility);

        #endregion Project Access Mappings

        #region User Mappings

        config.NewConfig<UserCreateDTO, User>()
            .MapWith(src => new User(new UserKey(src.Name),
            src.Issuer,
            src.Subject));

        #endregion User Mappings
    }
}
