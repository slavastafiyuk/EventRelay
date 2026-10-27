using EventRelay.ControlPlaneService.Domain.Entities.Project;
using EventRelay.ControlPlaneService.Domain.Entities.Project.Access;
using EventRelay.ControlPlaneService.Domain.Entities.User;
using EventRelay.ControlPlaneService.Infrastructure.Persistence.Models.Project;
using EventRelay.ControlPlaneService.Infrastructure.Persistence.Models.Project.Access;
using EventRelay.ControlPlaneService.Infrastructure.Persistence.Models.User;
using Mapster;

namespace EventRelay.ControlPlaneService.Repository.Mappings;

public sealed class Mapping : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        #region User Mappings

        config.NewConfig<User, UserRecord>()
            .Map(dest => dest.Id, src => src.Key.Id)
            .Map(dest => dest.Issuer, src => src.Issuer)
            .Map(dest => dest.Subject, src => src.Subject)
            .Map(dest => dest.Name, src => src.Key.Name);
        config.NewConfig<UserRecord, User>()
            .MapWith(src => new User(
                new UserKey(src.Id, src.Name),
                src.Issuer,
                src.Subject));
        config.NewConfig<UserKey, UserKeyRecord>();
        config.NewConfig<UserKeyRecord, UserKey>()
            .MapWith(src => new UserKey(src.Id, src.Name));

        #endregion User Mappings

        #region Project Mappings

        config.NewConfig<ProjectMember, ProjectMemberRecord>()
             .Map(dest => dest.UserId, src => src.User.Id)
             .Map(dest => dest.Role, src => src.Role)
             .Ignore(dest => dest.ProjectId)
             .Ignore(dest => dest.User);

        config.NewConfig<ProjectMemberRecord, ProjectMember>()
            .MapWith(src => new ProjectMember(
                src.User.Adapt<UserKey>(),
                src.Role));

        config.NewConfig<ProjectAccess, ProjectAccessRecord>()
            .Ignore(dest => dest.ProjectId)
            .Map(dest => dest.Visibility, src => src.Visibility)
            .Map(dest => dest.Members, src => src.Members);

        config.NewConfig<ProjectAccessRecord, ProjectAccess>()
            .MapWith(src => RestoreAccess(src));

        config.NewConfig<Project, ProjectRecord>()
            .Map(dest => dest.Id, src => src.Key.Id)
            .Map(dest => dest.Name, src => src.Key.Name)
            .Map(dest => dest.Description, src => src.Description)
            .Map(dest => dest.CreatedAt, src => src.CreatedAt)
            .Map(dest => dest.OwnerId, src => src.Owner.Id)
            .Ignore(dest => dest.Owner)
            .Map(dest => dest.Access, src => src.Access);

        config.NewConfig<ProjectRecord, Project>()
            .MapWith(src => ToDomain(src));


        #endregion Project Mappings
    }

    private static Project ToDomain(ProjectRecord record)
    {
        return Project.Restore(
            new ProjectKey(record.Id, record.Name),
            record.Owner.Adapt<UserKey>(),
            record.CreatedAt,
            record.Description,
            record.Access.Adapt<ProjectAccess>());
    }

    private static ProjectAccess RestoreAccess(ProjectAccessRecord record)
    {
        return ProjectAccess.Restore(
            record.Visibility,
            record.Members?.Select(m => new ProjectMember(
                new UserKey(m.User.Id, m.User.Name),
                m.Role)));
    }
}
