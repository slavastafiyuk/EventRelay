using EventRelay.ControlPlaneService.Application.Abstractions.Authentication;
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
            .MapWith(src => ToProjectDTO(src));
        config.NewConfig<ProjectKeyDTO, ProjectKey>()
            .MapToConstructor(true);
        config.NewConfig<ProjectDTO, Project>()
            .MapWith(src => Project.Restore(
                new ProjectKey(src.Key.Id, src.Key.Name),
                new UserKey(src.Owner.Id, src.Owner.Name),
                src.CreatedAt,
                src.Description,
                src.Access.Adapt<ProjectAccess>()));
        config.NewConfig<ProjectCreateDTO, Project>()
            .ConstructUsing(src => new Project(
                new ProjectKey(src.Name),
                MapContext.Current!.GetService<ICurrentUser>().UserKey));

        #endregion Project Mappings

        #region Project Access Mappings

        config.NewConfig<ProjectAccess, ProjectAccessDTO>()
            .Map(dest => dest.Visibility,
                 src => (ProjectVisibilityDTO)src.Visibility);

        config.NewConfig<ProjectAccessDTO, ProjectAccess>()
            .MapWith(src => ProjectAccess.Restore(
                (ProjectVisibility)src.Visibility,
                src.Members.Select(m => ToDomainMember(m))));

        config.NewConfig<ProjectMember, ProjectMemberDTO>()
            .Map(dest => dest.Role,
                 src => (ProjectRoleDTO)src.Role);

        config.NewConfig<ProjectMemberDTO, ProjectMember>()
            .MapWith(src => ToDomainMember(src));

        #endregion

        #region User Mappings

        config.NewConfig<UserCreateDTO, User>()
            .MapWith(src => new User(new UserKey(src.Name),
            src.Issuer,
            src.Subject));

        config.NewConfig<UserKeyDTO, UserKey>()
            .MapToConstructor(true);

        config.NewConfig<UserKey, UserKeyDTO>();

        #endregion User Mappings
    }

    private static ProjectMember ToDomainMember(ProjectMemberDTO dto)
    {
        return new ProjectMember(
            dto.User.Adapt<UserKey>(),
            (ProjectRole)dto.Role);
    }

    private static ProjectDTO ToProjectDTO(Project project)
    {
        return new ProjectDTO
        {
            Key = project.Key.Adapt<ProjectKeyDTO>(),
            Description = project.Description,
            Access = project.Access.Adapt<ProjectAccessDTO>(),
            Owner = project.Owner.Adapt<UserKeyDTO>(),
            CreatedAt = project.CreatedAt
        };
    }
}
