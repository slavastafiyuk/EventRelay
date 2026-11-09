using System.Text.Json;
using EventRelay.ControlPlaneService.Domain.Entities.Api;
using EventRelay.ControlPlaneService.Domain.Entities.Project;
using EventRelay.ControlPlaneService.Domain.Entities.Project.Access;
using EventRelay.ControlPlaneService.Domain.Entities.User;
using EventRelay.ControlPlaneService.Presentation.Contracts.Api;
using EventRelay.ControlPlaneService.Presentation.Contracts.Project;
using EventRelay.ControlPlaneService.Presentation.Contracts.Project.Access;
using EventRelay.ControlPlaneService.Presentation.Contracts.User;
using Mapster;

namespace EventRelay.ControlPlaneService.Tests.Unit.Mappings;

[Collection("Mapster mappings")]
public sealed class PresentationMappingTests
{
    [Fact]
    public void ProjectCreateDto_MapsOntoExistingProject_WithoutChangingOwnerOrKey()
    {
        var owner = new UserKey(Guid.CreateVersion7(), "owner");
        var key = new ProjectKey(Guid.CreateVersion7(), "MyStore");
        var project = new Project(key, owner);
        var dto = new ProjectCreateDTO
        {
            Name = "MyStore",
            Description = "Online store"
        };

        var mapped = dto.Adapt(project);

        Assert.Same(project, mapped);
        Assert.Equal(key.Id, mapped.Key.Id);
        Assert.Equal(key.Name, mapped.Key.Name);
        Assert.Equal(owner.Id, mapped.Owner.Id);
        Assert.Equal(owner.Name, mapped.Owner.Name);
        Assert.Equal(dto.Description, mapped.Description);
        Assert.Equal(ProjectVisibility.Private, mapped.Access.Visibility);
    }

    [Fact]
    public void Project_MapsToDto_IncludingOwnerAccessAndMembers()
    {
        var createdAt = new DateTime(2026, 10, 20, 12, 30, 0, DateTimeKind.Utc);
        var owner = new UserKey(Guid.CreateVersion7(), "owner");
        var member = new UserKey(Guid.CreateVersion7(), "editor");
        var project = new Project(
            new ProjectKey(Guid.CreateVersion7(), "MyStore"),
            owner,
            createdAt)
        {
            Description = "Online store"
        };

        project.ChangeVisibility(ProjectVisibility.Internal);
        project.AddMember(member, ProjectRole.Editor);

        var dto = project.Adapt<ProjectDTO>();
        Assert.Equal(project.Key.Id, dto.Key.Id);
        Assert.Equal(project.Key.Name, dto.Key.Name);
        Assert.Equal(project.Description, dto.Description);
        Assert.Equal(createdAt, dto.CreatedAt);
        Assert.Equal(owner.Id, dto.Owner.Id);
        Assert.Equal(owner.Name, dto.Owner.Name);
        Assert.Equal(ProjectVisibilityDTO.Internal, dto.Access.Visibility);

        var mappedMember = Assert.Single(dto.Access.Members);
        Assert.Equal(member.Id, mappedMember.User.Id);
        Assert.Equal(member.Name, mappedMember.User.Name);
        Assert.Equal(ProjectRoleDTO.Editor, mappedMember.Role);
    }

    [Fact]
    public void ProjectDto_MapsBackToDomain_PreservingOwnerAndMembers()
    {
        var owner = new UserKey(Guid.CreateVersion7(), "owner");
        var member = new UserKey(Guid.CreateVersion7(), "viewer");
        var original = new Project(
            new ProjectKey(Guid.CreateVersion7(), "Portal"),
            owner)
        {
            Description = "Customer portal"
        };

        original.ChangeVisibility(ProjectVisibility.Internal);
        original.AddMember(member, ProjectRole.Viewer);
        var dto = original.Adapt<ProjectDTO>();
        var restored = dto.Adapt<Project>();
        Assert.Equal(original.Key.Id, restored.Key.Id);
        Assert.Equal(original.Key.Name, restored.Key.Name);
        Assert.Equal(original.Owner.Id, restored.Owner.Id);
        Assert.Equal(original.Owner.Name, restored.Owner.Name);
        Assert.Equal(original.Description, restored.Description);
        Assert.Equal(original.CreatedAt, restored.CreatedAt);
        Assert.Equal(ProjectVisibility.Internal, restored.Access.Visibility);

        var restoredMember = Assert.Single(restored.Access.Members);
        Assert.Equal(member.Id, restoredMember.User.Id);
        Assert.Equal(ProjectRole.Viewer, restoredMember.Role);
    }

    [Fact]
    public void ProjectAccessDto_MapsToDomain_IncludingMemberIdentityAndRole()
    {
        var memberId = Guid.CreateVersion7();
        var dto = new ProjectAccessDTO
        {
            Visibility = ProjectVisibilityDTO.Internal,
            Members =
            [
                new ProjectMemberDTO
                {
                    User = new UserKeyDTO
                    {
                        Id = memberId,
                        Name = "editor"
                    },
                    Role = ProjectRoleDTO.Editor
                }
            ]
        };

        var access = dto.Adapt<ProjectAccess>();
        Assert.Equal(ProjectVisibility.Internal, access.Visibility);

        var member = Assert.Single(access.Members);
        Assert.Equal(memberId, member.User.Id);
        Assert.Equal("editor", member.User.Name);
        Assert.Equal(ProjectRole.Editor, member.Role);
    }

    [Fact]
    public void KeyDtos_MapToDomain_WithoutGeneratingNewIds()
    {
        var userId = Guid.CreateVersion7();
        var projectId = Guid.CreateVersion7();
        var user = new UserKeyDTO
        {
            Id = userId,
            Name = "owner"
        }.Adapt<UserKey>();

        var project = new ProjectKeyDTO
        {
            Id = projectId,
            Name = "MyStore"
        }.Adapt<ProjectKey>();

        Assert.Equal(userId, user.Id);
        Assert.Equal("owner", user.Name);
        Assert.Equal(projectId, project.Id);
        Assert.Equal("MyStore", project.Name);
    }

    [Fact]
    public void UserCreateDto_MapsToDomain()
    {
        var dto = new UserCreateDTO
        {
            Name = "owner",
            Issuer = "https://identity.example.test/realms/eventrelay",
            Subject = "user-123"
        };

        var user = dto.Adapt<User>();
        Assert.NotEqual(Guid.Empty, user.Key.Id);
        Assert.Equal(dto.Name, user.Key.Name);
        Assert.Equal(dto.Issuer, user.Issuer);
        Assert.Equal(dto.Subject, user.Subject);
    }


    [Fact]
    public void ApiKey_MapsToDto_WithoutExposingHash()
    {
        var (key, secret) = ApiKey.Generate("Production");

        var dto = key.Adapt<ApiKeyDTO>();

        Assert.Equal(key.Id, dto.Id);
        Assert.Equal(key.Name, dto.Name);
        Assert.Null(typeof(ApiKeyDTO).GetProperty("KeyHash"));
        Assert.Null(typeof(ApiKeyDTO).GetProperty("Secret"));
        Assert.Equal(ApiKey.Hash(secret), key.KeyHash);
    }


    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ProjectApi_MapsToDto_WithLifecycleAndAudit(
        bool revoked)
    {
        var project = new ProjectKey("MyStore");
        var creator = new UserKey("Alice");
        var revoker = new UserKey("Bob");

        var (api, _) = ProjectApi.Issue(
            project, creator, "Production");

        if (revoked)
            api.Revoke(revoker);

        var dto = api.Adapt<ProjectApiDTO>();

        Assert.Equal(api.Key.Id, dto.Key.Id);
        Assert.Equal("Production", dto.Key.Name);

        Assert.Equal(project.Id, dto.Project.Id);
        Assert.Equal(project.Name, dto.Project.Name);

        Assert.Equal(creator.Id, dto.CreatedBy.Id);
        Assert.Equal(creator.Name, dto.CreatedBy.Name);

        Assert.Equal(api.CreatedAt, dto.CreatedAt);
        Assert.Equal(api.RevokedAt, dto.RevokedAt);
        Assert.Equal(!revoked, dto.IsActive);

        if (revoked)
        {
            Assert.NotNull(dto.RevokedBy);
            Assert.Equal(revoker.Id, dto.RevokedBy.Id);
        }
        else
        {
            Assert.Null(dto.RevokedBy);
        }

        var json = JsonSerializer.Serialize(dto);

        Assert.DoesNotContain("KeyHash", json);
        Assert.DoesNotContain("Secret", json);
    }


    [Fact]
    public void IssuedApi_MapsToDto_WithOneTimeSecret()
    {
        var project = new ProjectKey("MyStore");
        var creator = new UserKey("Alice");

        var (api, secret) = ProjectApi.Issue(
            project, creator, "Production");

        var issued = new IssuedApi(api, secret);

        var dto = issued.Adapt<IssuedApiDTO>();

        Assert.Equal(secret, dto.Secret);

        Assert.Equal(api.Key.Id, dto.Api.Key.Id);
        Assert.Equal(api.Key.Name, dto.Api.Key.Name);

        Assert.Equal(project.Id, dto.Api.Project.Id);
        Assert.Equal(creator.Id, dto.Api.CreatedBy.Id);

        var json = JsonSerializer.Serialize(dto);

        Assert.Contains(secret, json);
        Assert.DoesNotContain("KeyHash", json);
    }

}
