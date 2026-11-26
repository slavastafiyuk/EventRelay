using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using EventRelay.ControlPlaneService.Domain.Entities.Project.Access;
using EventRelay.ControlPlaneService.Infrastructure.Persistence;
using EventRelay.ControlPlaneService.Infrastructure.Persistence.Models.Project.Access;
using EventRelay.ControlPlaneService.Presentation.Contracts.Organization;
using EventRelay.ControlPlaneService.Presentation.Contracts.Organization.Access;
using EventRelay.ControlPlaneService.Presentation.Contracts.Organization.Access.Actions;
using EventRelay.ControlPlaneService.Presentation.Contracts.Project;
using EventRelay.ControlPlaneService.Presentation.Contracts.Project.Access;
using EventRelay.ControlPlaneService.Presentation.Contracts.Project.Access.Actions;
using EventRelay.ControlPlaneService.Presentation.Contracts.User;
using EventRelay.ControlPlaneService.Tests.Helpers;
using Microsoft.EntityFrameworkCore;

namespace EventRelay.ControlPlaneService.Tests.Integration.ProjectTests;

public sealed class ProjectAccessControllerTests(
    CreatePostgresContainer fixture)
    : IClassFixture<CreatePostgresContainer>
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web)
        {
            Converters = { new JsonStringEnumConverter() }
        };

    private ControlPlaneDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ControlPlaneDbContext>()
            .UseNpgsql(fixture.ConnectionString)
            .Options;

        return new ControlPlaneDbContext(options);
    }
    private async Task<HttpResponseMessage> SendAsync(
    HttpMethod method,
    string path,
    string? subject,
    HttpContent? content = null)
    {
        using var request = new HttpRequestMessage(method, path);

        if (subject is not null)
            request.Headers.Add("X-Test-User", subject);

        request.Content = content;

        return await fixture.Client.SendAsync(request);
    }

    private Task<HttpResponseMessage> ManageMemberAsync(
        Guid id,
        string? subject,
        ProjectMemberActionDTO action)
    {
        return SendAsync(
            HttpMethod.Post,
            $"/api/projects/{id}/members/actions",
            subject,
            JsonContent.Create<ProjectMemberActionDTO>(
                action, options: JsonOptions));
    }

    private Task<HttpResponseMessage> ChangeVisibilityAsync(
        Guid id,
        string? subject,
        ProjectVisibilityDTO visibility)
    {
        return SendAsync(
            HttpMethod.Put,
            $"/api/projects/{id}/visibility",
            subject,
            JsonContent.Create(visibility, options: JsonOptions));
    }

    private Task<HttpResponseMessage> ManageOrganizationMemberAsync(
        Guid id,
        string subject,
        OrganizationMemberActionDTO action)
    {
        return SendAsync(
            HttpMethod.Post,
            $"/api/organizations/{id}/members/actions",
            subject,
            JsonContent.Create<OrganizationMemberActionDTO>(
                action, options: JsonOptions));
    }

    private static async Task ExpectStatusAsync(
        Task<HttpResponseMessage> request,
        HttpStatusCode expected)
    {
        using var response = await request;

        Assert.Equal(expected, response.StatusCode);
    }

    private async Task<UserKeyDTO> GetUserAsync(string subject)
    {
        await using var db = CreateDbContext();

        var user = await db.Users
            .AsNoTracking()
            .SingleAsync(x => x.Subject == subject);

        return new UserKeyDTO
        {
            Id = user.Id,
            Name = user.Name
        };
    }

    private async Task<Guid> CreateProjectAsync(
        string subject,
        OrganizationKeyDTO organization)
    {
        using var response = await SendAsync(
            HttpMethod.Post,
            "/api/projects",
            subject,
            JsonContent.Create(new ProjectCreateDTO
            {
                Name = $"Project-{Guid.NewGuid():N}",
                Organization = organization
            }));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var project = await response.Content
            .ReadFromJsonAsync<ProjectKeyDTO>(JsonOptions);

        return Assert.IsType<ProjectKeyDTO>(project).Id;
    }

    private sealed record Scenario(
    Guid ProjectId,
    OrganizationKeyDTO Organization,
    string OwnerSubject,
    string MemberSubject,
    UserKeyDTO Member);

    private async Task<Scenario> CreateScenarioAsync(
        OrganizationRoleDTO? organizationRole =
            OrganizationRoleDTO.Member)
    {
        var ownerSubject = $"owner-{Guid.NewGuid():N}";
        var memberSubject = $"member-{Guid.NewGuid():N}";

        var organization = await OrganizationTestHelper.CreateAsync(
            fixture.Client, ownerSubject);

        var projectId = await CreateProjectAsync(
            ownerSubject, organization);

        // Authenticate and provision the target user.
        // The new project is Private.
        await ExpectStatusAsync(
            SendAsync(
                HttpMethod.Get,
                $"/api/projects/{projectId}",
                memberSubject),
            HttpStatusCode.Forbidden);

        var member = await GetUserAsync(memberSubject);

        if (organizationRole is not null)
        {
            await ExpectStatusAsync(
                ManageOrganizationMemberAsync(
                    organization.Id,
                    ownerSubject,
                    new AddOrganizationMemberDTO
                    {
                        UserKey = member,
                        Role = organizationRole.Value
                    }),
                HttpStatusCode.NoContent);
        }

        return new Scenario(
            projectId,
            organization,
            ownerSubject,
            memberSubject,
            member);
    }

    private async Task AddProjectMemberAsync(
    Scenario scenario,
    ProjectRoleDTO role = ProjectRoleDTO.Viewer)
    {
        await ExpectStatusAsync(
            ChangeVisibilityAsync(
                scenario.ProjectId,
                scenario.OwnerSubject,
                ProjectVisibilityDTO.Internal),
            HttpStatusCode.NoContent);

        await ExpectStatusAsync(
            ManageMemberAsync(
                scenario.ProjectId,
                scenario.OwnerSubject,
                new AddProjectMemberDTO
                {
                    UserKey = scenario.Member,
                    Role = role
                }),
            HttpStatusCode.NoContent);
    }

    private async Task<ProjectAccessRecord> LoadAccessAsync(Guid id)
    {
        await using var db = CreateDbContext();

        return await db.Set<ProjectAccessRecord>()
            .AsNoTracking()
            .Include(x => x.Members)
            .SingleAsync(x => x.ProjectId == id);
    }

    private async Task AssertCannotManageAsync(
    Guid id,
    string? subject,
    UserKeyDTO target,
    HttpStatusCode expected)
    {
        await ExpectStatusAsync(
            ManageMemberAsync(id, subject,
                new AddProjectMemberDTO
                {
                    UserKey = target,
                    Role = ProjectRoleDTO.Editor
                }),
            expected);

        await ExpectStatusAsync(
            ManageMemberAsync(id, subject,
                new UpdateProjectMemberRoleDTO
                {
                    UserKey = target,
                    Role = ProjectRoleDTO.Editor
                }),
            expected);

        await ExpectStatusAsync(
            ManageMemberAsync(id, subject,
                new DeleteProjectMemberDTO
                {
                    UserKey = target
                }),
            expected);

        await ExpectStatusAsync(
            ChangeVisibilityAsync(
                id, subject, ProjectVisibilityDTO.Public),
            expected);
    }

    [Theory]
    [InlineData(ProjectRoleDTO.Viewer)]
    [InlineData(ProjectRoleDTO.Editor)]
    public async Task AddMember_Owner_PersistsMember(
    ProjectRoleDTO role)
    {
        var scenario = await CreateScenarioAsync();

        await AddProjectMemberAsync(scenario, role);

        var access = await LoadAccessAsync(scenario.ProjectId);
        var member = Assert.Single(access.Members);

        Assert.Equal(scenario.Member.Id, member.UserId);
        Assert.Equal((ProjectRole)role, member.Role);
        Assert.Equal(ProjectVisibility.Internal, access.Visibility);
    }

    [Theory]
    [InlineData(ProjectRoleDTO.Viewer, ProjectRoleDTO.Editor)]
    [InlineData(ProjectRoleDTO.Editor, ProjectRoleDTO.Viewer)]
    public async Task ChangeMemberRole_Owner_PersistsNewRole(
    ProjectRoleDTO initialRole,
    ProjectRoleDTO newRole)
    {
        var scenario = await CreateScenarioAsync();

        await AddProjectMemberAsync(scenario, initialRole);

        await ExpectStatusAsync(
            ManageMemberAsync(
                scenario.ProjectId,
                scenario.OwnerSubject,
                new UpdateProjectMemberRoleDTO
                {
                    UserKey = scenario.Member,
                    Role = newRole
                }),
            HttpStatusCode.NoContent);

        var access = await LoadAccessAsync(scenario.ProjectId);
        var member = Assert.Single(access.Members);

        Assert.Equal(scenario.Member.Id, member.UserId);
        Assert.Equal((ProjectRole)newRole, member.Role);
    }

    [Fact]
    public async Task RemoveMember_Owner_RemovesMembership()
    {
        var scenario = await CreateScenarioAsync();

        await AddProjectMemberAsync(scenario);

        await ExpectStatusAsync(
            ManageMemberAsync(
                scenario.ProjectId,
                scenario.OwnerSubject,
                new DeleteProjectMemberDTO
                {
                    UserKey = scenario.Member
                }),
            HttpStatusCode.NoContent);

        var access = await LoadAccessAsync(scenario.ProjectId);

        Assert.Empty(access.Members);
    }

    [Theory]
    [InlineData(ProjectVisibilityDTO.Private, 0)]
    [InlineData(ProjectVisibilityDTO.Public, 0)]
    [InlineData(ProjectVisibilityDTO.Internal, 1)]
    public async Task ChangeVisibility_Owner_PersistsVisibility(
    ProjectVisibilityDTO visibility,
    int expectedMembers)
    {
        var scenario = await CreateScenarioAsync();

        await AddProjectMemberAsync(scenario);

        await ExpectStatusAsync(
            ChangeVisibilityAsync(
                scenario.ProjectId,
                scenario.OwnerSubject,
                visibility),
            HttpStatusCode.NoContent);

        var access = await LoadAccessAsync(scenario.ProjectId);

        Assert.Equal((ProjectVisibility)visibility, access.Visibility);
        Assert.Equal(expectedMembers, access.Members.Count);
    }

    [Theory]
    [InlineData(OrganizationRoleDTO.Member, ProjectRoleDTO.Viewer)]
    [InlineData(OrganizationRoleDTO.Member, ProjectRoleDTO.Editor)]
    [InlineData(OrganizationRoleDTO.Admin, ProjectRoleDTO.Editor)]
    public async Task ManageAccess_NonOwner_ReturnsForbidden(
    OrganizationRoleDTO organizationRole,
    ProjectRoleDTO projectRole)
    {
        var scenario = await CreateScenarioAsync(organizationRole);

        await AddProjectMemberAsync(scenario, projectRole);

        await AssertCannotManageAsync(
            scenario.ProjectId,
            scenario.MemberSubject,
            scenario.Member,
            HttpStatusCode.Forbidden);

        var access = await LoadAccessAsync(scenario.ProjectId);

        Assert.Equal(ProjectVisibility.Internal, access.Visibility);
        Assert.Equal(
            (ProjectRole)projectRole,
            Assert.Single(access.Members).Role);
    }

    [Fact]
    public async Task AddMember_UserOutsideOrganization_ReturnsNotFound()
    {
        var scenario = await CreateScenarioAsync(null);

        await ExpectStatusAsync(
            ChangeVisibilityAsync(
                scenario.ProjectId,
                scenario.OwnerSubject,
                ProjectVisibilityDTO.Internal),
            HttpStatusCode.NoContent);

        await ExpectStatusAsync(
            ManageMemberAsync(
                scenario.ProjectId,
                scenario.OwnerSubject,
                new AddProjectMemberDTO
                {
                    UserKey = scenario.Member,
                    Role = ProjectRoleDTO.Viewer
                }),
            HttpStatusCode.NotFound);

        var access = await LoadAccessAsync(scenario.ProjectId);

        Assert.Empty(access.Members);
    }

    [Fact]
    public async Task ManageAccess_Outsider_ReturnsForbidden()
    {
        var scenario = await CreateScenarioAsync(null);

        await AssertCannotManageAsync(
            scenario.ProjectId,
            scenario.MemberSubject,
            scenario.Member,
            HttpStatusCode.Forbidden);

        var access = await LoadAccessAsync(scenario.ProjectId);

        Assert.Equal(ProjectVisibility.Private, access.Visibility);
        Assert.Empty(access.Members);
    }

    [Fact]
    public async Task ManageAccess_Unauthenticated_ReturnsUnauthorized()
    {
        var scenario = await CreateScenarioAsync();

        await AssertCannotManageAsync(
            scenario.ProjectId,
            null,
            scenario.Member,
            HttpStatusCode.Unauthorized);

        var access = await LoadAccessAsync(scenario.ProjectId);

        Assert.Equal(ProjectVisibility.Private, access.Visibility);
        Assert.Empty(access.Members);
    }

    [Fact]
    public async Task AddMember_Duplicate_ReturnsConflict()
    {
        var scenario = await CreateScenarioAsync();

        await AddProjectMemberAsync(scenario);

        await ExpectStatusAsync(
            ManageMemberAsync(
                scenario.ProjectId,
                scenario.OwnerSubject,
                new AddProjectMemberDTO
                {
                    UserKey = scenario.Member,
                    Role = ProjectRoleDTO.Editor
                }),
            HttpStatusCode.Conflict);

        var access = await LoadAccessAsync(scenario.ProjectId);

        Assert.Equal(
            ProjectRole.Viewer,
            Assert.Single(access.Members).Role);
    }

    [Fact]
    public async Task AddMember_ProjectOwner_ReturnsConflict()
    {
        var scenario = await CreateScenarioAsync();

        var owner = await GetUserAsync(scenario.OwnerSubject);

        await ExpectStatusAsync(
            ChangeVisibilityAsync(
                scenario.ProjectId,
                scenario.OwnerSubject,
                ProjectVisibilityDTO.Internal),
            HttpStatusCode.NoContent);

        await ExpectStatusAsync(
            ManageMemberAsync(
                scenario.ProjectId,
                scenario.OwnerSubject,
                new AddProjectMemberDTO
                {
                    UserKey = owner,
                    Role = ProjectRoleDTO.Editor
                }),
            HttpStatusCode.Conflict);

        var access = await LoadAccessAsync(scenario.ProjectId);

        Assert.Empty(access.Members);
    }

    [Theory]
    [InlineData(ProjectVisibilityDTO.Private)]
    [InlineData(ProjectVisibilityDTO.Public)]
    public async Task AddMember_NonInternalProject_ReturnsConflict(
    ProjectVisibilityDTO visibility)
    {
        var scenario = await CreateScenarioAsync();

        await ExpectStatusAsync(
            ChangeVisibilityAsync(
                scenario.ProjectId,
                scenario.OwnerSubject,
                visibility),
            HttpStatusCode.NoContent);

        await ExpectStatusAsync(
            ManageMemberAsync(
                scenario.ProjectId,
                scenario.OwnerSubject,
                new AddProjectMemberDTO
                {
                    UserKey = scenario.Member,
                    Role = ProjectRoleDTO.Viewer
                }),
            HttpStatusCode.Conflict);

        var access = await LoadAccessAsync(scenario.ProjectId);

        Assert.Empty(access.Members);
        Assert.Equal((ProjectVisibility)visibility, access.Visibility);
    }

    [Fact]
    public async Task ManageMember_NonexistentMembership_ReturnsConflict()
    {
        var scenario = await CreateScenarioAsync();

        await ExpectStatusAsync(
            ChangeVisibilityAsync(
                scenario.ProjectId,
                scenario.OwnerSubject,
                ProjectVisibilityDTO.Internal),
            HttpStatusCode.NoContent);

        await ExpectStatusAsync(
            ManageMemberAsync(
                scenario.ProjectId,
                scenario.OwnerSubject,
                new UpdateProjectMemberRoleDTO
                {
                    UserKey = scenario.Member,
                    Role = ProjectRoleDTO.Editor
                }),
            HttpStatusCode.Conflict);

        await ExpectStatusAsync(
            ManageMemberAsync(
                scenario.ProjectId,
                scenario.OwnerSubject,
                new DeleteProjectMemberDTO
                {
                    UserKey = scenario.Member
                }),
            HttpStatusCode.Conflict);

        var access = await LoadAccessAsync(scenario.ProjectId);

        Assert.Empty(access.Members);
    }

    [Fact]
    public async Task AddMember_InvalidRole_ReturnsBadRequest()
    {
        var scenario = await CreateScenarioAsync();

        await ExpectStatusAsync(
            ChangeVisibilityAsync(
                scenario.ProjectId,
                scenario.OwnerSubject,
                ProjectVisibilityDTO.Internal),
            HttpStatusCode.NoContent);

        await ExpectStatusAsync(
            ManageMemberAsync(
                scenario.ProjectId,
                scenario.OwnerSubject,
                new AddProjectMemberDTO
                {
                    UserKey = scenario.Member,
                    Role = (ProjectRoleDTO)999
                }),
            HttpStatusCode.BadRequest);

        var access = await LoadAccessAsync(scenario.ProjectId);

        Assert.Empty(access.Members);
    }

    [Fact]
    public async Task ChangeVisibility_InvalidValue_ReturnsBadRequest()
    {
        var scenario = await CreateScenarioAsync();

        await AddProjectMemberAsync(scenario);

        await ExpectStatusAsync(
            ChangeVisibilityAsync(
                scenario.ProjectId,
                scenario.OwnerSubject,
                (ProjectVisibilityDTO)999),
            HttpStatusCode.BadRequest);

        var access = await LoadAccessAsync(scenario.ProjectId);

        Assert.Equal(ProjectVisibility.Internal, access.Visibility);
        Assert.Single(access.Members);
    }

    [Fact]
    public async Task ManageAccess_ProjectNotFound_ReturnsNotFound()
    {
        var scenario = await CreateScenarioAsync();

        await AssertCannotManageAsync(
            Guid.CreateVersion7(),
            scenario.OwnerSubject,
            scenario.Member,
            HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AddMember_UserNotFound_ReturnsNotFound()
    {
        var scenario = await CreateScenarioAsync();

        await ExpectStatusAsync(
            ChangeVisibilityAsync(
                scenario.ProjectId,
                scenario.OwnerSubject,
                ProjectVisibilityDTO.Internal),
            HttpStatusCode.NoContent);

        await ExpectStatusAsync(
            ManageMemberAsync(
                scenario.ProjectId,
                scenario.OwnerSubject,
                new AddProjectMemberDTO
                {
                    UserKey = new UserKeyDTO
                    {
                        Id = Guid.CreateVersion7(),
                        Name = "Nonexistent user"
                    },
                    Role = ProjectRoleDTO.Viewer
                }),
            HttpStatusCode.NotFound);

        var access = await LoadAccessAsync(scenario.ProjectId);

        Assert.Empty(access.Members);
    }
}
