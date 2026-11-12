using EventRelay.ControlPlaneService.Application.Models;
using EventRelay.ControlPlaneService.Domain.Entities.Api;
using EventRelay.ControlPlaneService.Domain.Entities.Organization;
using EventRelay.ControlPlaneService.Domain.Entities.Organization.Access;
using EventRelay.ControlPlaneService.Domain.Entities.Project;
using EventRelay.ControlPlaneService.Domain.Entities.Project.Access;
using EventRelay.ControlPlaneService.Domain.Entities.User;
using EventRelay.ControlPlaneService.Presentation.Contracts.Api;
using EventRelay.ControlPlaneService.Presentation.Contracts.Organization;
using EventRelay.ControlPlaneService.Presentation.Contracts.Organization.Access;
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
            .Ignore(dest => dest.Owner);

        #endregion Project Mappings

        #region Project Access Mappings

        config.NewConfig<ProjectAccess, ProjectAccessDTO>()
            .MapWith(src => ToProjectAccessDTO(src));

        config.NewConfig<ProjectAccessDTO, ProjectAccess>()
            .MapWith(src => ToDomainAccess(src));

        config.NewConfig<ProjectMember, ProjectMemberDTO>()
            .MapWith(src => ToProjectMemberDTO(src));

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

        config.NewConfig<UserKey, UserKeyDTO>()
            .MapWith(src => new UserKeyDTO
            {
                Id = src.Id,
                Name = src.Name
            });

        #endregion User Mappings

        #region API Mappings

        config.NewConfig<ApiKey, ApiKeyDTO>()
            .Map(dest => dest.Id, src => src.Id)
            .Map(dest => dest.Name, src => src.Name);

        config.NewConfig<ProjectApi, ProjectApiDTO>()
            .MapWith(src => ToProjectApiDTO(src));

        config.NewConfig<IssuedApi, IssuedApiDTO>();

        #endregion


        #region Organization Mappings

        config.NewConfig<OrganizationKey, OrganizationKeyDTO>();

        config.NewConfig<OrganizationKeyDTO, OrganizationKey>()
            .MapWith(src => new OrganizationKey(src.Id, src.Name));

        config.NewConfig<OrganizationCreateDTO, OrganizationKey>()
            .MapWith(src => new OrganizationKey(src.Name));

        config.NewConfig<OrganizationMember, OrganizationMemberDTO>()
            .MapWith(src => ToOrganizationMemberDTO(src));

        config.NewConfig<OrganizationMemberDTO, OrganizationMember>()
            .MapWith(src => ToDomainMember(src));

        config.NewConfig<Organization, OrganizationDTO>()
            .MapWith(src => ToOrganizationDTO(src));

        config.NewConfig<OrganizationDTO, Organization>()
            .MapWith(src => ToDomainOrganization(src));

        #endregion Organization Mappings


    }

    private static ProjectMember ToDomainMember(ProjectMemberDTO dto)
    {
        return new ProjectMember(dto.User.Adapt<UserKey>(), (ProjectRole)dto.Role);
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

    private static ProjectMemberDTO ToProjectMemberDTO(ProjectMember member)
    {
        return new ProjectMemberDTO
        {
            User = member.User.Adapt<UserKeyDTO>(),
            Role = (ProjectRoleDTO)member.Role
        };
    }

    private static ProjectAccessDTO ToProjectAccessDTO(ProjectAccess access)
    {
        return new ProjectAccessDTO
        {
            Visibility = (ProjectVisibilityDTO)access.Visibility,
            Members = access.Members
                .Select(member => member.Adapt<ProjectMemberDTO>())
                .ToList()
        };
    }

    private static ProjectAccess ToDomainAccess(ProjectAccessDTO dto)
    {
        return ProjectAccess.Restore(
            (ProjectVisibility)dto.Visibility,
            dto.Members.Select(ToDomainMember));
    }


    private static ProjectApiDTO ToProjectApiDTO(ProjectApi api)
    {
        return new ProjectApiDTO
        {
            Key = api.Key.Adapt<ApiKeyDTO>(),
            Project = api.Project.Adapt<ProjectKeyDTO>(),

            CreatedBy = api.CreatedBy.Adapt<UserKeyDTO>(),

            RevokedBy = api.RevokedBy is null
                ? null
                : api.RevokedBy.Adapt<UserKeyDTO>(),

            CreatedAt = api.CreatedAt,
            RevokedAt = api.RevokedAt,
            IsActive = api.IsActive
        };
    }


    private static OrganizationDTO ToOrganizationDTO(Organization organization)
    {
        return new OrganizationDTO
        {
            Key = organization.Key.Adapt<OrganizationKeyDTO>(),
            Owner = organization.Owner.Adapt<UserKeyDTO>(),
            CreatedAt = organization.CreatedAt,
            Members = organization.Members
                .Select(m => m.Adapt<OrganizationMemberDTO>())
                .ToList()
        };
    }

    private static OrganizationMemberDTO ToOrganizationMemberDTO(
        OrganizationMember member)
    {
        return new OrganizationMemberDTO
        {
            User = member.User.Adapt<UserKeyDTO>(),
            Role = (OrganizationRoleDTO)member.Role,
            JoinedAt = member.JoinedAt
        };
    }

    private static OrganizationMember ToDomainMember(
        OrganizationMemberDTO dto)
    {
        return OrganizationMember.Restore(
            dto.User.Adapt<UserKey>(),
            (OrganizationRole)dto.Role,
            dto.JoinedAt);
    }

    private static Organization ToDomainOrganization(OrganizationDTO dto)
    {
        return Organization.Restore(
            dto.Key.Adapt<OrganizationKey>(),
            dto.Owner.Adapt<UserKey>(),
            dto.CreatedAt,
            dto.Members.Select(m => m.Adapt<OrganizationMember>()));
    }


}
