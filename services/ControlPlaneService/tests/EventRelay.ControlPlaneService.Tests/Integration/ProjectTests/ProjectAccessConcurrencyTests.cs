using System.Data;
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
using EventRelay.ControlPlaneService.Tests.Helpers.Interceptors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EventRelay.ControlPlaneService.Tests.Integration.ProjectTests;

public sealed class ProjectAccessConcurrencyTests(
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
        Guid ProjectId,
        OrganizationKeyDTO Organization,
        string OwnerSubject,
        string MemberSubject,
        UserKeyDTO Member);

    private sealed record ConcurrentOperationResult(
        HttpStatusCode Status,
        string? ProblemTitle);

    private ControlPlaneDbContext CreateDbContext()
    {
        var options =
            new DbContextOptionsBuilder<ControlPlaneDbContext>()
                .UseNpgsql(fixture.ConnectionString)
                .Options;

        return new ControlPlaneDbContext(options);
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string path,
        string subject,
        HttpContent? content = null,
        HttpClient? client = null,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(
            method,
            path);

        request.Headers.Add(
            "X-Test-User",
            subject);

        request.Content = content;

        return await (client ?? fixture.Client)
            .SendAsync(
                request,
                cancellationToken);
    }

    private Task<HttpResponseMessage> ManageMemberAsync(
        Guid projectId,
        string subject,
        ProjectMemberActionDTO action,
        HttpClient? client = null,
        CancellationToken cancellationToken = default)
    {
        return SendAsync(
            HttpMethod.Post,
            $"/api/projects/{projectId}/members/actions",
            subject,
            JsonContent.Create<ProjectMemberActionDTO>(
                action,
                options: JsonOptions),
            client,
            cancellationToken);
    }

    private Task<HttpResponseMessage> ChangeVisibilityAsync(
        Guid projectId,
        string subject,
        ProjectVisibilityDTO visibility,
        HttpClient? client = null,
        CancellationToken cancellationToken = default)
    {
        return SendAsync(
            HttpMethod.Put,
            $"/api/projects/{projectId}/visibility",
            subject,
            JsonContent.Create(
                visibility,
                options: JsonOptions),
            client,
            cancellationToken);
    }

    private Task<HttpResponseMessage> ManageOrganizationMemberAsync(
        Guid organizationId,
        string subject,
        OrganizationMemberActionDTO action,
        HttpClient? client = null,
        CancellationToken cancellationToken = default)
    {
        return SendAsync(
            HttpMethod.Post,
            $"/api/organizations/{organizationId}/members/actions",
            subject,
            JsonContent.Create<OrganizationMemberActionDTO>(
                action,
                options: JsonOptions),
            client,
            cancellationToken);
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

    private async Task<Guid> CreateProjectAsync(
        string subject,
        OrganizationKeyDTO organization)
    {
        using var response = await SendAsync(
            HttpMethod.Post,
            "/api/projects",
            subject,
            JsonContent.Create(
                new ProjectCreateDTO
                {
                    Name = $"Project-{Guid.NewGuid():N}",
                    Organization = organization
                },
                options: JsonOptions));

        Assert.Equal(
            HttpStatusCode.Created,
            response.StatusCode);

        var project = await response.Content
            .ReadFromJsonAsync<ProjectKeyDTO>(
                JsonOptions);

        return Assert.IsType<ProjectKeyDTO>(project).Id;
    }

    private async Task<Scenario> CreateScenarioAsync()
    {
        var ownerSubject =
            $"owner-{Guid.NewGuid():N}";

        var memberSubject =
            $"member-{Guid.NewGuid():N}";

        var organization =
            await OrganizationTestHelper.CreateAsync(
                fixture.Client,
                ownerSubject);

        var projectId = await CreateProjectAsync(
            ownerSubject,
            organization);

        // Authenticate and provision the target user.
        // The project is still Private.

        await ExpectStatusAsync(
            SendAsync(
                HttpMethod.Get,
                $"/api/projects/{projectId}",
                memberSubject),
            HttpStatusCode.Forbidden);

        var member = await GetUserAsync(memberSubject);

        await ExpectStatusAsync(
            ManageOrganizationMemberAsync(
                organization.Id,
                ownerSubject,
                new AddOrganizationMemberDTO
                {
                    UserKey = member,
                    Role = OrganizationRoleDTO.Member
                }),
            HttpStatusCode.NoContent);

        await ExpectStatusAsync(
            ChangeVisibilityAsync(
                projectId,
                ownerSubject,
                ProjectVisibilityDTO.Internal),
            HttpStatusCode.NoContent);

        return new Scenario(
            projectId,
            organization,
            ownerSubject,
            memberSubject,
            member);
    }

    private async Task<ProjectAccessRecord> LoadAccessAsync(
        Guid projectId)
    {
        await using var db = CreateDbContext();

        return await db.Set<ProjectAccessRecord>()
            .AsNoTracking()
            .Include(x => x.Members)
            .SingleAsync(
                x => x.ProjectId == projectId,
                Ct);
    }

    private Task<HttpResponseMessage> AddMemberAsync(
        Scenario scenario,
        ProjectRoleDTO role,
        HttpClient? client = null,
        CancellationToken cancellationToken = default)
    {
        return ManageMemberAsync(
            scenario.ProjectId,
            scenario.OwnerSubject,
            new AddProjectMemberDTO
            {
                UserKey = scenario.Member,
                Role = role
            },
            client,
            cancellationToken);
    }

    private async Task<ConcurrentOperationResult>
        RunConcurrentOperationsAsync(
            Guid projectId,
            Func<
                HttpClient,
                CancellationToken,
                Task<HttpResponseMessage>> firstOperation,
            Func<
                CancellationToken,
                Task<HttpResponseMessage>> secondOperation)
    {
        var barrier =
            new ProjectReadBarrierInterceptor(projectId);

        using var factory =
            new ApiFactory(fixture.ConnectionString);

        using var interceptedFactory =
            factory.WithWebHostBuilder(
                builder =>
                {
                    builder.ConfigureTestServices(
                        services =>
                        {
                            services.AddDbContext<
                                ControlPlaneDbContext>(
                                options =>
                                    options.AddInterceptors(barrier));
                        });
                });

        using var interceptedClient =
            interceptedFactory.CreateClient();

        interceptedClient.Timeout =
            TimeSpan.FromSeconds(60);

        using var cancellation =
            new CancellationTokenSource(
                TimeSpan.FromSeconds(60));

        var firstTask = firstOperation(
            interceptedClient,
            cancellation.Token);

        HttpStatusCode firstStatus = default;

        string? problemTitle = null;

        try
        {
            // Wait until the first request has established
            // its PostgreSQL transaction snapshot.

            var isolationLevel =
                await barrier.Entered.WaitAsync(
                    TimeSpan.FromSeconds(20),
                    cancellation.Token);

            Assert.Equal(
                IsolationLevel.Serializable,
                isolationLevel);

            // Execute the second request while the first
            // request is suspended.

            using var secondResponse =
                await secondOperation(
                    cancellation.Token);

            Assert.Equal(
                HttpStatusCode.NoContent,
                secondResponse.StatusCode);
        }
        finally
        {
            // Always release the suspended request,
            // including when an assertion fails.

            barrier.Release();

            using var firstResponse =
                await firstTask.WaitAsync(
                    TimeSpan.FromSeconds(30));

            firstStatus = firstResponse.StatusCode;

            if (firstStatus == HttpStatusCode.Conflict)
            {
                var problem = await firstResponse.Content
                    .ReadFromJsonAsync<ProblemDetails>(
                        JsonOptions);

                problemTitle = problem?.Title;
            }
        }

        return new ConcurrentOperationResult(
            firstStatus,
            problemTitle);
    }

    [Theory]
    [InlineData(
        true,
        ProjectVisibilityDTO.Private)]
    [InlineData(
        false,
        ProjectVisibilityDTO.Private)]
    [InlineData(
        true,
        ProjectVisibilityDTO.Public)]
    [InlineData(
        false,
        ProjectVisibilityDTO.Public)]
    public async Task ConcurrentMembershipAndVisibilityChanges_PreserveInvariant(
        bool addStartsFirst,
        ProjectVisibilityDTO visibility)
    {
        var scenario = await CreateScenarioAsync();

        var result = await RunConcurrentOperationsAsync(
            scenario.ProjectId,

            (client, ct) => addStartsFirst
                ? AddMemberAsync(
                    scenario,
                    ProjectRoleDTO.Viewer,
                    client,
                    ct)
                : ChangeVisibilityAsync(
                    scenario.ProjectId,
                    scenario.OwnerSubject,
                    visibility,
                    client,
                    ct),

            ct => addStartsFirst
                ? ChangeVisibilityAsync(
                    scenario.ProjectId,
                    scenario.OwnerSubject,
                    visibility,
                    cancellationToken: ct)
                : AddMemberAsync(
                    scenario,
                    ProjectRoleDTO.Viewer,
                    cancellationToken: ct));

        Assert.Equal(
            HttpStatusCode.Conflict,
            result.Status);

        Assert.Equal(
            "Transaction conflict.",
            result.ProblemTitle);

        var access = await LoadAccessAsync(
            scenario.ProjectId);

        if (addStartsFirst)
        {
            // The visibility change committed while
            // the member addition was suspended.

            Assert.Equal(
                (ProjectVisibility)visibility,
                access.Visibility);

            Assert.Empty(access.Members);
        }
        else
        {
            // The member addition committed while
            // the visibility change was suspended.

            Assert.Equal(
                ProjectVisibility.Internal,
                access.Visibility);

            var member = Assert.Single(access.Members);

            Assert.Equal(
                scenario.Member.Id,
                member.UserId);

            Assert.Equal(
                ProjectRole.Viewer,
                member.Role);
        }
    }

    [Fact]
    public async Task ConcurrentDuplicateMemberAddition_PersistsOnlyOneMember()
    {
        var scenario = await CreateScenarioAsync();

        var result = await RunConcurrentOperationsAsync(
            scenario.ProjectId,

            (client, ct) => AddMemberAsync(
                scenario,
                ProjectRoleDTO.Editor,
                client,
                ct),

            ct => AddMemberAsync(
                scenario,
                ProjectRoleDTO.Viewer,
                cancellationToken: ct));

        Assert.Equal(
            HttpStatusCode.Conflict,
            result.Status);

        Assert.Contains(
            result.ProblemTitle,
            new[]
            {
                "Transaction conflict.",
                "Project membership conflict."
            });

        var access = await LoadAccessAsync(
            scenario.ProjectId);

        Assert.Equal(
            ProjectVisibility.Internal,
            access.Visibility);

        var member = Assert.Single(access.Members);

        Assert.Equal(
            scenario.Member.Id,
            member.UserId);

        Assert.Equal(
            ProjectRole.Viewer,
            member.Role);
    }

    [Fact]
    public async Task ConcurrentOrganizationRevocationAndAddition_HasValidSerialOutcome()
    {
        var scenario = await CreateScenarioAsync();

        var result = await RunConcurrentOperationsAsync(
            scenario.ProjectId,

            (client, ct) => AddMemberAsync(
                scenario,
                ProjectRoleDTO.Viewer,
                client,
                ct),

            ct => ManageOrganizationMemberAsync(
                scenario.Organization.Id,
                scenario.OwnerSubject,
                new DeleteOrganizationMemberDTO
                {
                    UserKey = scenario.Member
                },
                cancellationToken: ct));

        Assert.Contains(
            result.Status,
            new[]
            {
                HttpStatusCode.NoContent,
                HttpStatusCode.Conflict
            });

        await using (var db = CreateDbContext())
        {
            var organization = await db.Organizations
                .AsNoTracking()
                .Include(x => x.Members)
                .SingleAsync(
                    x => x.Id == scenario.Organization.Id,
                    Ct);

            Assert.DoesNotContain(
                organization.Members,
                member =>
                    member.UserId == scenario.Member.Id);
        }

        var access = await LoadAccessAsync(
            scenario.ProjectId);

        Assert.Equal(
            ProjectVisibility.Internal,
            access.Visibility);

        if (result.Status == HttpStatusCode.NoContent)
        {
            // PostgreSQL may serialize the project addition
            // before the organization revocation.

            var member = Assert.Single(access.Members);

            Assert.Equal(
                scenario.Member.Id,
                member.UserId);

            Assert.Equal(
                ProjectRole.Viewer,
                member.Role);
        }
        else
        {
            Assert.Equal(
                "Transaction conflict.",
                result.ProblemTitle);

            Assert.Empty(access.Members);
        }

        // Every new operation that starts after revocation
        // must observe the user's current membership state.

        await ExpectStatusAsync(
            ManageMemberAsync(
                scenario.ProjectId,
                scenario.OwnerSubject,
                new UpdateProjectMemberRoleDTO
                {
                    UserKey = scenario.Member,
                    Role = ProjectRoleDTO.Editor
                }),
            HttpStatusCode.NotFound);

        var finalAccess = await LoadAccessAsync(
            scenario.ProjectId);

        if (result.Status == HttpStatusCode.NoContent)
        {
            Assert.Equal(
                ProjectRole.Viewer,
                Assert.Single(finalAccess.Members).Role);
        }
        else
        {
            Assert.Empty(finalAccess.Members);
        }
    }
}
