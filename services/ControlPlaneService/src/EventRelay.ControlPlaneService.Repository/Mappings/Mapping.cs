using EventRelay.ControlPlaneService.Domain.Entities.Api;
using EventRelay.ControlPlaneService.Domain.Entities.Organization;
using EventRelay.ControlPlaneService.Domain.Entities.Organization.Access;
using EventRelay.ControlPlaneService.Domain.Entities.Project;
using EventRelay.ControlPlaneService.Domain.Entities.Project.Access;
using EventRelay.ControlPlaneService.Domain.Entities.User;
using EventRelay.ControlPlaneService.Infrastructure.Persistence.Models.Api;
using EventRelay.ControlPlaneService.Infrastructure.Persistence.Models.Organization;
using EventRelay.ControlPlaneService.Infrastructure.Persistence.Models.Project;
using EventRelay.ControlPlaneService.Infrastructure.Persistence.Models.Project.Access;
using EventRelay.ControlPlaneService.Infrastructure.Persistence.Models.User;
using Mapster;

namespace EventRelay.ControlPlaneService.Repository.Mappings;

public sealed class Mapping : IRegister
{
    public void Register(TypeAdapterConfig config)
    {

        #region Organization Mappings

        config.NewConfig<OrganizationMember, OrganizationMemberRecord>()
            .Map(dest => dest.UserId, src => src.User.Id)
            .Map(dest => dest.Role, src => src.Role)
            .Map(dest => dest.JoinedAt, src => src.JoinedAt)
            .Ignore(dest => dest.OrganizationId)
            .Ignore(dest => dest.User);

        config.NewConfig<OrganizationMemberRecord, OrganizationMember>()
            .MapWith(src => OrganizationMember.Restore(
                src.User.Adapt<UserKey>(),
                src.Role,
                src.JoinedAt));

        config.NewConfig<Organization, OrganizationRecord>()
            .Map(dest => dest.Id, src => src.Key.Id)
            .Map(dest => dest.Name, src => src.Key.Name)
            .Map(dest => dest.CreatedAt, src => src.CreatedAt)
            .Map(dest => dest.OwnerId, src => src.Owner.Id)
            .Ignore(dest => dest.Owner)
            .Map(dest => dest.Members, src => src.Members);

        config.NewConfig<OrganizationRecord, Organization>()
            .MapWith(src => ToDomain(src));

        #endregion Organization Mappings

        #region Organization Key Mappings

        config.NewConfig<OrganizationKey, OrganizationKeyRecord>();

        config.NewConfig<OrganizationKeyRecord, OrganizationKey>()
            .MapWith(src => new OrganizationKey(src.Id, src.Name));

        #endregion Organization Key Mappings

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
            .MapWith(src => ToDomainAccess(src));

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

        #region Project Key Mappings

        config.NewConfig<ProjectKey, ProjectKeyRecord>();

        config.NewConfig<ProjectKeyRecord, ProjectKey>()
            .MapWith(src => new ProjectKey(src.Id, src.Name));

        #endregion

        #region Project API Mappings

        config.NewConfig<ProjectApi, ProjectApiRecord>()
            .Map(dest => dest.Id, src => src.Key.Id)
            .Map(dest => dest.Name, src => src.Key.Name)
            .Map(dest => dest.KeyHash, src => src.Key.KeyHash)
            .Map(dest => dest.ProjectId, src => src.Project.Id)
            .Map(dest => dest.CreatedByUserId, src => src.CreatedBy.Id)
            .Map(dest => dest.RevokedByUserId,
                src => src.RevokedBy == null
                    ? (Guid?)null
                    : src.RevokedBy.Id)
            .Ignore(dest => dest.Project)
            .Ignore(dest => dest.CreatedBy)
            .Ignore(dest => dest.RevokedBy);

        config.NewConfig<ProjectApiRecord, ProjectApi>()
            .MapWith(src => ProjectApi.Restore(ApiKey.Restore(src.Id, src.Name, src.KeyHash),
                src.Project.Adapt<ProjectKey>(),
                src.CreatedBy.Adapt<UserKey>(),
                src.CreatedAt,
                src.RevokedBy == null ? null : src.RevokedBy.Adapt<UserKey>(),
                src.RevokedAt));

        #endregion

    }

    private static Organization ToDomain(OrganizationRecord record)
    {
        return Organization.Restore(
            new OrganizationKey(record.Id, record.Name),
            record.Owner.Adapt<UserKey>(),
            record.CreatedAt,
            record.Members
                .Select(m => m.Adapt<OrganizationMember>()));
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

    private static ProjectAccess ToDomainAccess(ProjectAccessRecord record)
    {
        var members = record.Members?
            .Select(member => new ProjectMember(
                new UserKey(member.User.Id, member.User.Name),
                member.Role))
            .ToList();

        return ProjectAccess.Restore(
            record.Visibility,
            members);
    }
}
