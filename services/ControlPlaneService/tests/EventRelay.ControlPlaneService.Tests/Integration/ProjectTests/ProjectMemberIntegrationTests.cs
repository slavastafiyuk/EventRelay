using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using EventRelay.ControlPlaneService.Domain.Entities.Api;
using EventRelay.ControlPlaneService.Domain.Entities.Organization;
using EventRelay.ControlPlaneService.Domain.Entities.Project;
using EventRelay.ControlPlaneService.Domain.Entities.Project.Access;
using EventRelay.ControlPlaneService.Domain.Entities.User;
using EventRelay.ControlPlaneService.Infrastructure.Persistence;
using EventRelay.ControlPlaneService.Infrastructure.Persistence.Models.Organization;
using EventRelay.ControlPlaneService.Infrastructure.Persistence.Models.Project.Access;
using EventRelay.ControlPlaneService.Presentation.Contracts.Api;
using EventRelay.ControlPlaneService.Presentation.Contracts.Organization;
using EventRelay.ControlPlaneService.Presentation.Contracts.Organization.Access;
using EventRelay.ControlPlaneService.Presentation.Contracts.Project;
using EventRelay.ControlPlaneService.Presentation.Contracts.Project.Access;
using EventRelay.ControlPlaneService.Presentation.Contracts.User;
using EventRelay.ControlPlaneService.Repository.Repositories;
using EventRelay.ControlPlaneService.Tests.Helpers;
using Microsoft.EntityFrameworkCore;

namespace EventRelay.ControlPlaneService.Tests.Integration.ProjectTests;

public sealed class ProjectMemberIntegrationTests(
    CreatePostgresContainer fixture)
    : IClassFixture<CreatePostgresContainer>
{
    private static readonly CancellationToken Ct =
        CancellationToken.None;

    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web)
        {
            Converters =
            {
                new JsonStringEnumConverter()
            }
        };

    private sealed record Scenario(
        OrganizationKeyDTO Organization,
        Guid ProjectId,
        string OrganizationOwnerSubject,
        string ProjectOwnerSubject);

    #region Helpers

    private ControlPlaneDbContext CreateDbContext()
    {
        var options =
            new DbContextOptionsBuilder<ControlPlaneDbContext>()
                .UseNpgsql(fixture.ConnectionString)
                .Options;

        return new ControlPlaneDbContext(options);
    }

    private async Task<HttpResponseMessage> SendAsAsync(
        HttpMethod method,
        string path,
        string subject,
        object? body = null)
    {
        using var request = new HttpRequestMessage(
            method,
            path);

        request.Headers.Add(
            "X-Test-User",
            subject);

        if (body is not null)
        {
            request.Content = JsonContent.Create(
                body,
                options: JsonOptions);
        }

        return await fixture.Client.SendAsync(
            request,
            Ct);
    }

    private static async Task ExpectStatusAsync(
        Task<HttpResponseMessage> request,
        HttpStatusCode expected)
    {
        using var response = await request;

        Assert.Equal(
            expected,
            response.StatusCode);
    }

    private async Task<UserKeyDTO> GetUserAsync(
        string subject)
    {
        await using var db = CreateDbContext();

        var user = await db.Users
            .AsNoTracking()
            .SingleAsync(
                x => x.Subject == subject,
                Ct);

        return new UserKeyDTO
        {
            Id = user.Id,
            Name = user.Name
        };
    }

    private async Task<UserKeyDTO> ProvisionUserAsync(
        string subject)
    {
        await OrganizationTestHelper.CreateAsync(
            fixture.Client,
            subject);

        return await GetUserAsync(subject);
    }

    private async Task AddOrganizationMemberAsync(
        OrganizationKeyDTO organization,
        string ownerSubject,
        UserKeyDTO member,
        OrganizationRoleDTO role = OrganizationRoleDTO.Member)
    {
        await ExpectStatusAsync(
            SendAsAsync(
                HttpMethod.Post,
                $"/api/organizations/{organization.Id}/members/actions",
                ownerSubject,
                new
                {
                    action = "AddNewMember",
                    userKey = member,
                    role
                }),
            HttpStatusCode.NoContent);
    }

    private async Task<Guid> CreateProjectAsync(
        string ownerSubject,
        OrganizationKeyDTO organization)
    {
        using var response = await SendAsAsync(
            HttpMethod.Post,
            "/api/projects",
            ownerSubject,
            new ProjectCreateDTO
            {
                Name = $"Project-{Guid.NewGuid():N}",
                Organization = organization
            });

        Assert.Equal(
            HttpStatusCode.Created,
            response.StatusCode);

        var project = await response.Content
            .ReadFromJsonAsync<ProjectKeyDTO>(JsonOptions);

        return Assert.IsType<ProjectKeyDTO>(project).Id;
    }

    private async Task<Scenario> CreateScenarioAsync(
        bool useSeparateProjectOwner = false)
    {
        var organizationOwnerSubject =
            $"organization-owner-{Guid.NewGuid():N}";

        var organization =
            await OrganizationTestHelper.CreateAsync(
                fixture.Client,
                organizationOwnerSubject);

        var projectOwnerSubject =
            organizationOwnerSubject;

        if (useSeparateProjectOwner)
        {
            projectOwnerSubject =
                $"project-owner-{Guid.NewGuid():N}";

            var projectOwner =
                await ProvisionUserAsync(
                    projectOwnerSubject);

            await AddOrganizationMemberAsync(
                organization,
                organizationOwnerSubject,
                projectOwner);
        }

        var projectId = await CreateProjectAsync(
            projectOwnerSubject,
            organization);

        return new Scenario(
            organization,
            projectId,
            organizationOwnerSubject,
            projectOwnerSubject);
    }

    private Task<HttpResponseMessage> ReadProjectAsync(
        Guid projectId,
        string subject)
    {
        return SendAsAsync(
            HttpMethod.Get,
            $"/api/projects/{projectId}",
            subject);
    }

    private Task<HttpResponseMessage> ChangeVisibilityAsync(
        Scenario scenario,
        ProjectVisibilityDTO visibility)
    {
        return SendAsAsync(
            HttpMethod.Put,
            $"/api/projects/{scenario.ProjectId}/visibility",
            scenario.ProjectOwnerSubject,
            visibility);
    }

    private async Task<UserKeyDTO> AddProjectMemberAsync(
        Scenario scenario,
        string memberSubject,
        ProjectRoleDTO role)
    {
        var member = await ProvisionUserAsync(
            memberSubject);

        await AddOrganizationMemberAsync(
            scenario.Organization,
            scenario.OrganizationOwnerSubject,
            member);

        await ExpectStatusAsync(
            ChangeVisibilityAsync(
                scenario,
                ProjectVisibilityDTO.Internal),
            HttpStatusCode.NoContent);

        await ExpectStatusAsync(
            SendAsAsync(
                HttpMethod.Post,
                $"/api/projects/{scenario.ProjectId}/members/actions",
                scenario.ProjectOwnerSubject,
                new
                {
                    action = "AddNewMember",
                    userKey = member,
                    role
                }),
            HttpStatusCode.NoContent);

        return member;
    }

    private async Task RemoveOrganizationMembershipInDatabaseAsync(
        Guid organizationId,
        Guid userId)
    {
        // Intentionally simulate stale or inconsistent data.
        // This bypasses application-level membership cleanup.

        await using var db = CreateDbContext();

        var deleted = await db
            .Set<OrganizationMemberRecord>()
            .Where(x =>
                x.OrganizationId == organizationId &&
                x.UserId == userId)
            .ExecuteDeleteAsync(Ct);

        Assert.Equal(1, deleted);
    }

    private async Task<IssuedApiDTO> IssueApiKeyAsync(
        Scenario scenario)
    {
        using var response = await SendAsAsync(
            HttpMethod.Post,
            $"/api/projects/{scenario.ProjectId}/api-keys",
            scenario.ProjectOwnerSubject,
            new CreateApiKeyDTO
            {
                Name = "Integration key"
            });

        Assert.Equal(
            HttpStatusCode.Created,
            response.StatusCode);

        var issued = await response.Content
            .ReadFromJsonAsync<IssuedApiDTO>(
                JsonOptions);

        return Assert.IsType<IssuedApiDTO>(issued);
    }

    private async Task<HttpResponseMessage> ResolveApiKeyAsync(
        string secret)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "/internal/api-keys/resolve")
        {
            Content = JsonContent.Create(
                new ResolveApiKeyDTO
                {
                    KeyHash = ApiKey.Hash(secret)
                })
        };

        request.Headers.Add(
            "X-Internal-Token",
            ApiFactory.InternalServiceTokenForTests);

        return await fixture.Client.SendAsync(
            request,
            Ct);
    }

    #endregion Helpers

    #region Persistence Tests

    [Fact]
    public async Task ProjectWithMembers_SaveAndReload_PreservesMembersAndRoles()
    {
        var scenario = await CreateScenarioAsync();

        var owner = await GetUserAsync(
            scenario.ProjectOwnerSubject);

        var member = await ProvisionUserAsync(
            $"member-{Guid.NewGuid():N}");

        await AddOrganizationMemberAsync(
            scenario.Organization,
            scenario.OrganizationOwnerSubject,
            member);

        var ownerKey = new UserKey(
            owner.Id,
            owner.Name);

        var memberKey = new UserKey(
            member.Id,
            member.Name);

        var organizationKey = new OrganizationKey(
            scenario.Organization.Id,
            scenario.Organization.Name);

        var project = new Project(
            new ProjectKey(
                $"MemberRoundTrip-{Guid.NewGuid():N}"),
            ownerKey,
            organizationKey);

        project.ChangeVisibility(
            ProjectVisibility.Internal);

        project.AddMember(
            memberKey,
            ProjectRole.Editor);

        await using (var db = CreateDbContext())
        {
            var repository = new ProjectRepository(db);

            await repository.AddAsync(project, Ct);

            await db.SaveChangesAsync(Ct);
        }

        await using (var db = CreateDbContext())
        {
            var repository = new ProjectRepository(db);

            var reloaded = await repository.GetByIdAsync(
                project.Key.Id,
                Ct);

            Assert.NotNull(reloaded);

            Assert.NotNull(reloaded.Organization);

            Assert.Equal(
                scenario.Organization.Id,
                reloaded.Organization.Id);

            Assert.Equal(
                ProjectVisibility.Internal,
                reloaded.Access.Visibility);

            var actualMember =
                Assert.Single(reloaded.Access.Members);

            Assert.Equal(
                member.Id,
                actualMember.User.Id);

            Assert.Equal(
                member.Name,
                actualMember.User.Name);

            Assert.Equal(
                ProjectRole.Editor,
                actualMember.Role);
        }
    }

    #endregion Persistence Tests

    #region Project Authorization Tests

    [Theory]
    [InlineData(ProjectRoleDTO.Viewer)]
    [InlineData(ProjectRoleDTO.Editor)]
    public async Task GetById_ActiveProjectMember_Returns200(
        ProjectRoleDTO role)
    {
        var scenario = await CreateScenarioAsync();

        var memberSubject =
            $"member-{Guid.NewGuid():N}";

        var member = await AddProjectMemberAsync(
            scenario,
            memberSubject,
            role);

        using var ownerResponse =
            await ReadProjectAsync(
                scenario.ProjectId,
                scenario.ProjectOwnerSubject);

        Assert.Equal(
            HttpStatusCode.OK,
            ownerResponse.StatusCode);

        var project = await ownerResponse.Content
            .ReadFromJsonAsync<ProjectDTO>(
                JsonOptions);

        Assert.NotNull(project);

        Assert.Equal(
            ProjectVisibilityDTO.Internal,
            project.Access.Visibility);

        var projectMember =
            Assert.Single(project.Access.Members);

        Assert.Equal(
            member.Id,
            projectMember.User.Id);

        Assert.Equal(
            role,
            projectMember.Role);

        await ExpectStatusAsync(
            ReadProjectAsync(
                scenario.ProjectId,
                memberSubject),
            HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetById_RemovedProjectMember_CannotReadInternalProject()
    {
        var scenario = await CreateScenarioAsync();

        var memberSubject =
            $"member-{Guid.NewGuid():N}";

        var member = await AddProjectMemberAsync(
            scenario,
            memberSubject,
            ProjectRoleDTO.Viewer);

        await ExpectStatusAsync(
            ReadProjectAsync(
                scenario.ProjectId,
                memberSubject),
            HttpStatusCode.OK);

        await ExpectStatusAsync(
            SendAsAsync(
                HttpMethod.Post,
                $"/api/projects/{scenario.ProjectId}/members/actions",
                scenario.ProjectOwnerSubject,
                new
                {
                    action = "Delete",
                    userKey = member
                }),
            HttpStatusCode.NoContent);

        await ExpectStatusAsync(
            ReadProjectAsync(
                scenario.ProjectId,
                memberSubject),
            HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData(ProjectRoleDTO.Viewer)]
    [InlineData(ProjectRoleDTO.Editor)]
    public async Task GetById_StaleProjectMembership_DoesNotGrantAccess(
        ProjectRoleDTO role)
    {
        var scenario = await CreateScenarioAsync();

        var memberSubject =
            $"member-{Guid.NewGuid():N}";

        var member = await AddProjectMemberAsync(
            scenario,
            memberSubject,
            role);

        await ExpectStatusAsync(
            ReadProjectAsync(
                scenario.ProjectId,
                memberSubject),
            HttpStatusCode.OK);

        // Deliberately leave the project membership in place.

        await RemoveOrganizationMembershipInDatabaseAsync(
            scenario.Organization.Id,
            member.Id);

        await using (var db = CreateDbContext())
        {
            Assert.True(
                await db.Set<ProjectMemberRecord>()
                    .AnyAsync(
                        x => x.ProjectId == scenario.ProjectId &&
                             x.UserId == member.Id,
                        Ct));
        }

        // Project permissions alone must not grant access.

        await ExpectStatusAsync(
            ReadProjectAsync(
                scenario.ProjectId,
                memberSubject),
            HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData(OrganizationRoleDTO.Member)]
    [InlineData(OrganizationRoleDTO.Admin)]
    public async Task GetById_OrganizationMemberWithoutProjectRole_Returns403(
        OrganizationRoleDTO role)
    {
        var scenario = await CreateScenarioAsync();

        var subject =
            $"organization-member-{Guid.NewGuid():N}";

        var member = await ProvisionUserAsync(subject);

        await AddOrganizationMemberAsync(
            scenario.Organization,
            scenario.OrganizationOwnerSubject,
            member,
            role);

        await ExpectStatusAsync(
            ChangeVisibilityAsync(
                scenario,
                ProjectVisibilityDTO.Internal),
            HttpStatusCode.NoContent);

        await ExpectStatusAsync(
            ReadProjectAsync(
                scenario.ProjectId,
                subject),
            HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetById_PublicProject_AllowsExternalAuthenticatedUser()
    {
        var scenario = await CreateScenarioAsync();

        var outsiderSubject =
            $"outsider-{Guid.NewGuid():N}";

        await ExpectStatusAsync(
            ChangeVisibilityAsync(
                scenario,
                ProjectVisibilityDTO.Public),
            HttpStatusCode.NoContent);

        await ExpectStatusAsync(
            ReadProjectAsync(
                scenario.ProjectId,
                outsiderSubject),
            HttpStatusCode.OK);
    }

    [Theory]
    [InlineData(ProjectVisibilityDTO.Private)]
    [InlineData(ProjectVisibilityDTO.Internal)]
    [InlineData(ProjectVisibilityDTO.Public)]
    public async Task GetById_FormerOwner_OnlyRetainsPublicReadAccess(
        ProjectVisibilityDTO visibility)
    {
        var scenario = await CreateScenarioAsync(
            useSeparateProjectOwner: true);

        var projectOwner = await GetUserAsync(
            scenario.ProjectOwnerSubject);

        await ExpectStatusAsync(
            ChangeVisibilityAsync(
                scenario,
                visibility),
            HttpStatusCode.NoContent);

        await RemoveOrganizationMembershipInDatabaseAsync(
            scenario.Organization.Id,
            projectOwner.Id);

        var expected =
            visibility == ProjectVisibilityDTO.Public
                ? HttpStatusCode.OK
                : HttpStatusCode.NotFound;

        await ExpectStatusAsync(
            ReadProjectAsync(
                scenario.ProjectId,
                scenario.ProjectOwnerSubject),
            expected);
    }

    #endregion Project Authorization Tests

    #region API-Key Authorization Tests

    [Theory]
    [InlineData(ProjectVisibilityDTO.Private)]
    [InlineData(ProjectVisibilityDTO.Internal)]
    [InlineData(ProjectVisibilityDTO.Public)]
    public async Task FormerProjectOwner_CannotManageApiKeys(
        ProjectVisibilityDTO visibility)
    {
        var scenario = await CreateScenarioAsync(
            useSeparateProjectOwner: true);

        var projectOwner = await GetUserAsync(
            scenario.ProjectOwnerSubject);

        var issued = await IssueApiKeyAsync(
            scenario);

        await ExpectStatusAsync(
            ChangeVisibilityAsync(
                scenario,
                visibility),
            HttpStatusCode.NoContent);

        await RemoveOrganizationMembershipInDatabaseAsync(
            scenario.Organization.Id,
            projectOwner.Id);

        await ExpectStatusAsync(
            SendAsAsync(
                HttpMethod.Post,
                $"/api/projects/{scenario.ProjectId}/api-keys",
                scenario.ProjectOwnerSubject,
                new CreateApiKeyDTO
                {
                    Name = "Unauthorized key"
                }),
            HttpStatusCode.NotFound);

        await ExpectStatusAsync(
            SendAsAsync(
                HttpMethod.Delete,
                $"/api/projects/{scenario.ProjectId}/api-keys/{issued.Api.Key.Id}",
                scenario.ProjectOwnerSubject),
            HttpStatusCode.NotFound);

        // The rejected revocation must not disable
        // the existing project credential.

        using var response = await ResolveApiKeyAsync(
            issued.Secret);

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        var resolved = await response.Content
            .ReadFromJsonAsync<ResolvedApiKeyDTO>(
                JsonOptions);

        Assert.Equal(
            scenario.ProjectId,
            Assert.IsType<ResolvedApiKeyDTO>(resolved).ProjectId);
    }

    [Fact]
    public async Task PublicProject_ExternalUser_CannotIssueOrRevokeApiKeys()
    {
        var scenario = await CreateScenarioAsync();

        var issued = await IssueApiKeyAsync(
            scenario);

        var outsiderSubject =
            $"outsider-{Guid.NewGuid():N}";

        await ExpectStatusAsync(
            ChangeVisibilityAsync(
                scenario,
                ProjectVisibilityDTO.Public),
            HttpStatusCode.NoContent);

        await ExpectStatusAsync(
            ReadProjectAsync(
                scenario.ProjectId,
                outsiderSubject),
            HttpStatusCode.OK);

        await ExpectStatusAsync(
            SendAsAsync(
                HttpMethod.Post,
                $"/api/projects/{scenario.ProjectId}/api-keys",
                outsiderSubject,
                new CreateApiKeyDTO
                {
                    Name = "Unauthorized key"
                }),
            HttpStatusCode.Forbidden);

        await ExpectStatusAsync(
            SendAsAsync(
                HttpMethod.Delete,
                $"/api/projects/{scenario.ProjectId}/api-keys/{issued.Api.Key.Id}",
                outsiderSubject),
            HttpStatusCode.Forbidden);

        await ExpectStatusAsync(
            ResolveApiKeyAsync(issued.Secret),
            HttpStatusCode.OK);
    }

    #endregion API-Key Authorization Tests
}
