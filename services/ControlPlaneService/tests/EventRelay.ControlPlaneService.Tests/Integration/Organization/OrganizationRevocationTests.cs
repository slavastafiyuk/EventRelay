using System.Data;
using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using EventRelay.ControlPlaneService.Domain.Entities.Project.Access;
using EventRelay.ControlPlaneService.Domain.Exceptions.Organization;
using EventRelay.ControlPlaneService.Infrastructure.Persistence;
using EventRelay.ControlPlaneService.Infrastructure.Persistence.Models.Organization;
using EventRelay.ControlPlaneService.Infrastructure.Persistence.Models.Project.Access;
using EventRelay.ControlPlaneService.Presentation.Contracts.Organization;
using EventRelay.ControlPlaneService.Presentation.Contracts.Organization.Access;
using EventRelay.ControlPlaneService.Presentation.Contracts.Project;
using EventRelay.ControlPlaneService.Presentation.Contracts.Project.Access;
using EventRelay.ControlPlaneService.Presentation.Contracts.User;
using EventRelay.ControlPlaneService.Tests.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace EventRelay.ControlPlaneService.Tests.Integration.OrganizationTests;

public sealed class OrganizationRevocationTests(
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
        string OwnerSubject,
        string MemberSubject,
        UserKeyDTO Member);

    #region Helpers

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
        object? body = null,
        HttpClient? client = null,
        CancellationToken cancellationToken = default)
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

        return await (client ?? fixture.Client)
            .SendAsync(
                request,
                cancellationToken);
    }

    private static async Task ExpectStatusAsync(
        Task<HttpResponseMessage> request,
        HttpStatusCode expected)
    {
        using var response = await request;

        var body = await response.Content.ReadAsStringAsync();

        Assert.True(
            response.StatusCode == expected,
            $"Expected {(int)expected} ({expected}), " +
            $"received {(int)response.StatusCode} " +
            $"({response.StatusCode}): {body}");
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

    private async Task<Scenario> CreateScenarioAsync(
        string? existingMemberSubject = null)
    {
        var ownerSubject =
            $"owner-{Guid.NewGuid():N}";

        var memberSubject =
            existingMemberSubject ??
            $"member-{Guid.NewGuid():N}";

        var organization =
            await OrganizationTestHelper.CreateAsync(
                fixture.Client,
                ownerSubject);

        if (existingMemberSubject is null)
        {
            await OrganizationTestHelper.CreateAsync(
                fixture.Client,
                memberSubject);
        }

        var member = await GetUserAsync(memberSubject);

        var scenario = new Scenario(
            organization,
            ownerSubject,
            memberSubject,
            member);

        await ExpectStatusAsync(
            AddOrganizationMemberAsync(scenario),
            HttpStatusCode.NoContent);

        return scenario;
    }

    private Task<HttpResponseMessage>
        AddOrganizationMemberAsync(
            Scenario scenario,
            OrganizationRoleDTO role =
                OrganizationRoleDTO.Member)
    {
        return SendAsync(
            HttpMethod.Post,
            $"/api/organizations/{scenario.Organization.Id}/members/actions",
            scenario.OwnerSubject,
            new
            {
                action = "AddNewMember",
                userKey = scenario.Member,
                role
            });
    }

    private Task<HttpResponseMessage>
        RemoveOrganizationMemberAsync(
            Scenario scenario,
            HttpClient? client = null,
            CancellationToken cancellationToken = default)
    {
        return SendAsync(
            HttpMethod.Post,
            $"/api/organizations/{scenario.Organization.Id}/members/actions",
            scenario.OwnerSubject,
            new
            {
                action = "Delete",
                userKey = scenario.Member
            },
            client,
            cancellationToken);
    }

    private Task<HttpResponseMessage> PostProjectAsync(
        Scenario scenario,
        string name,
        string subject,
        HttpClient? client = null,
        CancellationToken cancellationToken = default)
    {
        return SendAsync(
            HttpMethod.Post,
            "/api/projects",
            subject,
            new ProjectCreateDTO
            {
                Name = name,
                Organization = scenario.Organization
            },
            client,
            cancellationToken);
    }

    private async Task<Guid> CreateProjectAsync(
        Scenario scenario,
        string? subject = null)
    {
        using var response = await PostProjectAsync(
            scenario,
            $"Project-{Guid.NewGuid():N}",
            subject ?? scenario.OwnerSubject);

        Assert.Equal(
            HttpStatusCode.Created,
            response.StatusCode);

        var project = await response.Content
            .ReadFromJsonAsync<ProjectKeyDTO>(
                JsonOptions);

        return Assert.IsType<ProjectKeyDTO>(project).Id;
    }

    private Task<HttpResponseMessage> ChangeVisibilityAsync(
        Guid projectId,
        string subject,
        ProjectVisibilityDTO visibility)
    {
        return SendAsync(
            HttpMethod.Put,
            $"/api/projects/{projectId}/visibility",
            subject,
            visibility);
    }

    private async Task<Guid> CreateInternalProjectAsync(
        Scenario scenario)
    {
        var projectId = await CreateProjectAsync(scenario);

        await ExpectStatusAsync(
            ChangeVisibilityAsync(
                projectId,
                scenario.OwnerSubject,
                ProjectVisibilityDTO.Internal),
            HttpStatusCode.NoContent);

        return projectId;
    }

    private Task<HttpResponseMessage> AddProjectMemberAsync(
        Scenario scenario,
        Guid projectId,
        ProjectRoleDTO role = ProjectRoleDTO.Viewer,
        HttpClient? client = null,
        CancellationToken cancellationToken = default)
    {
        return SendAsync(
            HttpMethod.Post,
            $"/api/projects/{projectId}/members/actions",
            scenario.OwnerSubject,
            new
            {
                action = "AddNewMember",
                userKey = scenario.Member,
                role
            },
            client,
            cancellationToken);
    }

    private async Task<Guid>
        CreateInternalProjectWithMemberAsync(
            Scenario scenario,
            ProjectRoleDTO role = ProjectRoleDTO.Viewer)
    {
        var projectId =
            await CreateInternalProjectAsync(scenario);

        await ExpectStatusAsync(
            AddProjectMemberAsync(
                scenario,
                projectId,
                role),
            HttpStatusCode.NoContent);

        return projectId;
    }

    private Task<HttpResponseMessage> ReadProjectAsync(
        Guid projectId,
        string subject)
    {
        return SendAsync(
            HttpMethod.Get,
            $"/api/projects/{projectId}",
            subject);
    }

    private async Task<bool> IsOrganizationMemberAsync(
        Scenario scenario)
    {
        await using var db = CreateDbContext();

        return await db.Set<OrganizationMemberRecord>()
            .AsNoTracking()
            .AnyAsync(
                x => x.OrganizationId ==
                         scenario.Organization.Id &&
                     x.UserId == scenario.Member.Id,
                Ct);
    }

    private async Task<bool> IsProjectMemberAsync(
        Guid projectId,
        Guid userId)
    {
        await using var db = CreateDbContext();

        return await db.Set<ProjectMemberRecord>()
            .AsNoTracking()
            .AnyAsync(
                x => x.ProjectId == projectId &&
                     x.UserId == userId,
                Ct);
    }

    private async Task<int> CountOwnedProjectsAsync(
        Scenario scenario)
    {
        await using var db = CreateDbContext();

        return await db.Projects
            .AsNoTracking()
            .CountAsync(
                x => x.OrganizationId ==
                         scenario.Organization.Id &&
                     x.OwnerId == scenario.Member.Id,
                Ct);
    }

    private async Task RemoveMembershipDirectlyAsync(
        Scenario scenario)
    {
        // Simulate legacy or inconsistent database state.
        // Bypass normal application-level cleanup deliberately.

        await using var db = CreateDbContext();

        var deleted = await db
            .Set<OrganizationMemberRecord>()
            .Where(x =>
                x.OrganizationId ==
                    scenario.Organization.Id &&
                x.UserId == scenario.Member.Id)
            .ExecuteDeleteAsync(Ct);

        Assert.Equal(1, deleted);
    }

    private async Task<(
        HttpStatusCode Status,
        string? ProblemTitle)> RunConcurrentOperationsAsync(
            Guid organizationId,
            Func<
                HttpClient,
                CancellationToken,
                Task<HttpResponseMessage>> firstOperation,
            Func<
                CancellationToken,
                Task<HttpResponseMessage>> secondOperation,
            HttpStatusCode secondExpectedStatus)
    {
        var barrier =
            new OrganizationReadBarrierInterceptor(
                organizationId);

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
                                    options.AddInterceptors(
                                        barrier));
                        });
                });

        using var client =
            interceptedFactory.CreateClient();

        client.Timeout = TimeSpan.FromSeconds(60);

        using var cancellation =
            new CancellationTokenSource(
                TimeSpan.FromSeconds(60));

        var firstTask = firstOperation(
            client,
            cancellation.Token);

        HttpStatusCode firstStatus = default;

        string? problemTitle = null;

        try
        {
            var isolationLevel =
                await barrier.Entered.WaitAsync(
                    TimeSpan.FromSeconds(20),
                    cancellation.Token);

            Assert.Equal(
                IsolationLevel.Serializable,
                isolationLevel);

            // The first transaction has established
            // its PostgreSQL snapshot.
            //
            // Execute and commit the competing operation
            // before releasing the first transaction.

            using var secondResponse =
                await secondOperation(
                    cancellation.Token);

            var secondBody =
                await secondResponse.Content
                    .ReadAsStringAsync();

            Assert.True(
                secondResponse.StatusCode ==
                    secondExpectedStatus,
                $"Expected {(int)secondExpectedStatus}, " +
                $"received {(int)secondResponse.StatusCode}: " +
                secondBody);
        }
        finally
        {
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

        return (firstStatus, problemTitle);
    }

    #endregion Helpers

    #region Membership Cleanup Tests

    [Theory]
    [InlineData(OrganizationRoleDTO.Member)]
    [InlineData(OrganizationRoleDTO.Admin)]
    public async Task RemoveOrganizationMember_RemovesAllProjectMemberships(
        OrganizationRoleDTO role)
    {
        var scenario = await CreateScenarioAsync();

        if (role == OrganizationRoleDTO.Admin)
        {
            await ExpectStatusAsync(
                SendAsync(
                    HttpMethod.Post,
                    $"/api/organizations/{scenario.Organization.Id}/members/actions",
                    scenario.OwnerSubject,
                    new
                    {
                        action = "SetAdmin",
                        userKey = scenario.Member
                    }),
                HttpStatusCode.NoContent);
        }

        var firstProject =
            await CreateInternalProjectWithMemberAsync(
                scenario,
                ProjectRoleDTO.Viewer);

        var secondProject =
            await CreateInternalProjectWithMemberAsync(
                scenario,
                ProjectRoleDTO.Editor);

        await ExpectStatusAsync(
            ReadProjectAsync(
                firstProject,
                scenario.MemberSubject),
            HttpStatusCode.OK);

        await ExpectStatusAsync(
            ReadProjectAsync(
                secondProject,
                scenario.MemberSubject),
            HttpStatusCode.OK);

        await ExpectStatusAsync(
            RemoveOrganizationMemberAsync(scenario),
            HttpStatusCode.NoContent);

        Assert.False(
            await IsOrganizationMemberAsync(scenario));

        Assert.False(
            await IsProjectMemberAsync(
                firstProject,
                scenario.Member.Id));

        Assert.False(
            await IsProjectMemberAsync(
                secondProject,
                scenario.Member.Id));

        await ExpectStatusAsync(
            ReadProjectAsync(
                firstProject,
                scenario.MemberSubject),
            HttpStatusCode.Forbidden);

        await ExpectStatusAsync(
            ReadProjectAsync(
                secondProject,
                scenario.MemberSubject),
            HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task RemoveOrganizationMember_DoesNotAffectOtherOrganizations()
    {
        var firstOrganization =
            await CreateScenarioAsync();

        var secondOrganization =
            await CreateScenarioAsync(
                firstOrganization.MemberSubject);

        var firstProject =
            await CreateInternalProjectWithMemberAsync(
                firstOrganization,
                ProjectRoleDTO.Viewer);

        var secondProject =
            await CreateInternalProjectWithMemberAsync(
                secondOrganization,
                ProjectRoleDTO.Editor);

        // Ownership in another organization must not
        // prevent removal from the first organization.

        var externallyOwnedProject =
            await CreateProjectAsync(
                secondOrganization,
                secondOrganization.MemberSubject);

        await ExpectStatusAsync(
            RemoveOrganizationMemberAsync(
                firstOrganization),
            HttpStatusCode.NoContent);

        Assert.False(
            await IsOrganizationMemberAsync(
                firstOrganization));

        Assert.False(
            await IsProjectMemberAsync(
                firstProject,
                firstOrganization.Member.Id));

        Assert.True(
            await IsOrganizationMemberAsync(
                secondOrganization));

        Assert.True(
            await IsProjectMemberAsync(
                secondProject,
                secondOrganization.Member.Id));

        await ExpectStatusAsync(
            ReadProjectAsync(
                secondProject,
                secondOrganization.MemberSubject),
            HttpStatusCode.OK);

        await ExpectStatusAsync(
            ReadProjectAsync(
                externallyOwnedProject,
                secondOrganization.MemberSubject),
            HttpStatusCode.OK);
    }

    [Fact]
    public async Task RejoinOrganization_DoesNotRestorePreviousProjectRoles()
    {
        var scenario = await CreateScenarioAsync();

        var projectId =
            await CreateInternalProjectWithMemberAsync(
                scenario,
                ProjectRoleDTO.Editor);

        await ExpectStatusAsync(
            ReadProjectAsync(
                projectId,
                scenario.MemberSubject),
            HttpStatusCode.OK);

        await ExpectStatusAsync(
            RemoveOrganizationMemberAsync(scenario),
            HttpStatusCode.NoContent);

        await ExpectStatusAsync(
            AddOrganizationMemberAsync(scenario),
            HttpStatusCode.NoContent);

        Assert.True(
            await IsOrganizationMemberAsync(scenario));

        Assert.False(
            await IsProjectMemberAsync(
                projectId,
                scenario.Member.Id));

        await ExpectStatusAsync(
            ReadProjectAsync(
                projectId,
                scenario.MemberSubject),
            HttpStatusCode.Forbidden);

        // Project access must be explicitly granted again.

        await ExpectStatusAsync(
            AddProjectMemberAsync(
                scenario,
                projectId,
                ProjectRoleDTO.Viewer),
            HttpStatusCode.NoContent);

        await using (var db = CreateDbContext())
        {
            var membership =
                await db.Set<ProjectMemberRecord>()
                    .AsNoTracking()
                    .SingleAsync(
                        x => x.ProjectId == projectId &&
                             x.UserId == scenario.Member.Id,
                        Ct);

            Assert.Equal(
                ProjectRole.Viewer,
                membership.Role);
        }

        await ExpectStatusAsync(
            ReadProjectAsync(
                projectId,
                scenario.MemberSubject),
            HttpStatusCode.OK);
    }

    [Fact]
    public async Task CreateProject_AfterOrganizationRemoval_Returns404()
    {
        var scenario = await CreateScenarioAsync();

        await ExpectStatusAsync(
            RemoveOrganizationMemberAsync(scenario),
            HttpStatusCode.NoContent);

        await ExpectStatusAsync(
            PostProjectAsync(
                scenario,
                $"Forbidden-{Guid.NewGuid():N}",
                scenario.MemberSubject),
            HttpStatusCode.NotFound);

        Assert.Equal(
            0,
            await CountOwnedProjectsAsync(scenario));
    }

    #endregion Membership Cleanup Tests

    #region Ownership Protection Tests

    [Fact]
    public async Task RemoveOrganizationMember_WhoOwnsProjects_Returns409()
    {
        var scenario = await CreateScenarioAsync();

        var sharedProject =
            await CreateInternalProjectWithMemberAsync(
                scenario,
                ProjectRoleDTO.Editor);

        var ownedProject =
            await CreateProjectAsync(
                scenario,
                scenario.MemberSubject);

        using var response =
            await RemoveOrganizationMemberAsync(scenario);

        Assert.Equal(
            HttpStatusCode.Conflict,
            response.StatusCode);

        var problem = await response.Content
            .ReadFromJsonAsync<ProblemDetails>(
                JsonOptions);

        Assert.NotNull(problem);

        Assert.Equal(
            "Organization member owns projects.",
            problem.Title);

        // A rejected removal must not change anything.

        Assert.True(
            await IsOrganizationMemberAsync(scenario));

        Assert.True(
            await IsProjectMemberAsync(
                sharedProject,
                scenario.Member.Id));

        Assert.Equal(
            1,
            await CountOwnedProjectsAsync(scenario));

        await ExpectStatusAsync(
            ReadProjectAsync(
                ownedProject,
                scenario.MemberSubject),
            HttpStatusCode.OK);

        await ExpectStatusAsync(
            ReadProjectAsync(
                sharedProject,
                scenario.MemberSubject),
            HttpStatusCode.OK);
    }

    #endregion Ownership Protection Tests

    #region Rollback Tests

    [Fact]
    public async Task RemoveOrganizationMember_WhenPersistenceFails_RollsBackCleanup()
    {
        var scenario = await CreateScenarioAsync();

        var projectId =
            await CreateInternalProjectWithMemberAsync(
                scenario);

        var interceptor =
            new OrganizationRemovalFailureInterceptor(
                scenario.Organization.Id,
                scenario.Member.Id,
                projectId);

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
                                    options.AddInterceptors(
                                        interceptor));
                        });
                });

        using var client =
            interceptedFactory.CreateClient();

        await ExpectStatusAsync(
            RemoveOrganizationMemberAsync(
                scenario,
                client),
            HttpStatusCode.Conflict);

        // Verify the failure was injected only after
        // project membership cleanup had executed
        // inside the current transaction.

        Assert.True(interceptor.FailureInjected);

        // Verify committed state with independent DbContexts.

        Assert.True(
            await IsOrganizationMemberAsync(scenario));

        Assert.True(
            await IsProjectMemberAsync(
                projectId,
                scenario.Member.Id));

        await ExpectStatusAsync(
            ReadProjectAsync(
                projectId,
                scenario.MemberSubject),
            HttpStatusCode.OK);
    }

    #endregion Rollback Tests

    #region Historical Data Tests

    [Fact]
    public async Task StaleProjectMembership_CanBeRemovedButNotUpdated()
    {
        var scenario = await CreateScenarioAsync();

        var projectId =
            await CreateInternalProjectWithMemberAsync(
                scenario,
                ProjectRoleDTO.Viewer);

        // Bypass the new cleanup mechanism intentionally.

        await RemoveMembershipDirectlyAsync(scenario);

        await ExpectStatusAsync(
            SendAsync(
                HttpMethod.Post,
                $"/api/projects/{projectId}/members/actions",
                scenario.OwnerSubject,
                new
                {
                    action = "UpdateRole",
                    userKey = scenario.Member,
                    role = ProjectRoleDTO.Editor
                }),
            HttpStatusCode.NotFound);

        Assert.True(
            await IsProjectMemberAsync(
                projectId,
                scenario.Member.Id));

        await ExpectStatusAsync(
            SendAsync(
                HttpMethod.Post,
                $"/api/projects/{projectId}/members/actions",
                scenario.OwnerSubject,
                new
                {
                    action = "Delete",
                    userKey = scenario.Member
                }),
            HttpStatusCode.NoContent);

        Assert.False(
            await IsProjectMemberAsync(
                projectId,
                scenario.Member.Id));
    }

    [Fact]
    public async Task LegacyFormerProjectOwner_CannotManageProject()
    {
        var scenario = await CreateScenarioAsync();

        var projectId = await CreateProjectAsync(
            scenario,
            scenario.MemberSubject);

        var organizationOwner =
            await GetUserAsync(scenario.OwnerSubject);

        // Normal API removal is now prohibited.
        // Simulate historical inconsistent data instead.

        await RemoveMembershipDirectlyAsync(scenario);

        await ExpectStatusAsync(
            ReadProjectAsync(
                projectId,
                scenario.MemberSubject),
            HttpStatusCode.NotFound);

        await ExpectStatusAsync(
            ChangeVisibilityAsync(
                projectId,
                scenario.MemberSubject,
                ProjectVisibilityDTO.Public),
            HttpStatusCode.NotFound);

        var actions = new object[]
        {
            new
            {
                action = "AddNewMember",
                userKey = organizationOwner,
                role = ProjectRoleDTO.Viewer
            },
            new
            {
                action = "UpdateRole",
                userKey = organizationOwner,
                role = ProjectRoleDTO.Editor
            },
            new
            {
                action = "Delete",
                userKey = organizationOwner
            }
        };

        foreach (var action in actions)
        {
            await ExpectStatusAsync(
                SendAsync(
                    HttpMethod.Post,
                    $"/api/projects/{projectId}/members/actions",
                    scenario.MemberSubject,
                    action),
                HttpStatusCode.NotFound);
        }
    }

    #endregion Historical Data Tests

    #region Concurrency Tests

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConcurrentProjectCreationAndOrganizationRemoval_CannotOrphanProject(
        bool removalStartsFirst)
    {
        var scenario = await CreateScenarioAsync();

        var projectName =
            $"Concurrent-{Guid.NewGuid():N}";

        var result = await RunConcurrentOperationsAsync(
            scenario.Organization.Id,

            (client, ct) => removalStartsFirst
                ? RemoveOrganizationMemberAsync(
                    scenario,
                    client,
                    ct)
                : PostProjectAsync(
                    scenario,
                    projectName,
                    scenario.MemberSubject,
                    client,
                    ct),

            ct => removalStartsFirst
                ? PostProjectAsync(
                    scenario,
                    projectName,
                    scenario.MemberSubject,
                    cancellationToken: ct)
                : RemoveOrganizationMemberAsync(
                    scenario,
                    cancellationToken: ct),

            removalStartsFirst
                ? HttpStatusCode.Created
                : HttpStatusCode.NoContent);

        Assert.Equal(
            HttpStatusCode.Conflict,
            result.Status);

        Assert.Equal(
            "Transaction conflict.",
            result.ProblemTitle);

        if (removalStartsFirst)
        {
            // Project creation committed first.
            // Organization removal must roll back.

            Assert.True(
                await IsOrganizationMemberAsync(
                    scenario));

            Assert.Equal(
                1,
                await CountOwnedProjectsAsync(
                    scenario));
        }
        else
        {
            // Organization removal committed first.
            // Project creation must roll back.

            Assert.False(
                await IsOrganizationMemberAsync(
                    scenario));

            Assert.Equal(
                0,
                await CountOwnedProjectsAsync(
                    scenario));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConcurrentProjectMembershipAndOrganizationRemoval_CannotLeaveStaleMembership(
        bool removalStartsFirst)
    {
        var scenario = await CreateScenarioAsync();

        var projectId =
            await CreateInternalProjectAsync(scenario);

        var result = await RunConcurrentOperationsAsync(
            scenario.Organization.Id,

            (client, ct) => removalStartsFirst
                ? RemoveOrganizationMemberAsync(
                    scenario,
                    client,
                    ct)
                : AddProjectMemberAsync(
                    scenario,
                    projectId,
                    ProjectRoleDTO.Editor,
                    client,
                    ct),

            ct => removalStartsFirst
                ? AddProjectMemberAsync(
                    scenario,
                    projectId,
                    ProjectRoleDTO.Editor,
                    cancellationToken: ct)
                : RemoveOrganizationMemberAsync(
                    scenario,
                    cancellationToken: ct),

            HttpStatusCode.NoContent);

        Assert.Equal(
            HttpStatusCode.Conflict,
            result.Status);

        Assert.Equal(
            "Transaction conflict.",
            result.ProblemTitle);

        if (removalStartsFirst)
        {
            // Addition committed, removal conflicted.

            Assert.True(
                await IsOrganizationMemberAsync(
                    scenario));

            Assert.True(
                await IsProjectMemberAsync(
                    projectId,
                    scenario.Member.Id));

            // Retrying removal in a fresh transaction
            // must clean up the committed membership.

            await ExpectStatusAsync(
                RemoveOrganizationMemberAsync(
                    scenario),
                HttpStatusCode.NoContent);
        }

        Assert.False(
            await IsOrganizationMemberAsync(
                scenario));

        Assert.False(
            await IsProjectMemberAsync(
                projectId,
                scenario.Member.Id));
    }

    #endregion Concurrency Tests

    #region Test Interceptors

    private sealed class OrganizationReadBarrierInterceptor(
        Guid organizationId) : DbCommandInterceptor
    {
        private readonly TaskCompletionSource<IsolationLevel?>
            _entered = new(
                TaskCreationOptions
                    .RunContinuationsAsynchronously);

        private readonly TaskCompletionSource
            _released = new(
                TaskCreationOptions
                    .RunContinuationsAsynchronously);

        private int _claimed;

        public Task<IsolationLevel?> Entered =>
            _entered.Task;

        public void Release()
        {
            _released.TrySetResult();
        }

        public override async ValueTask<DbDataReader>
            ReaderExecutedAsync(
                DbCommand command,
                CommandExecutedEventData eventData,
                DbDataReader result,
                CancellationToken cancellationToken = default)
        {
            if (!IsOrganizationQuery(command))
                return result;

            if (Interlocked.CompareExchange(
                    ref _claimed,
                    1,
                    0) != 0)
            {
                return result;
            }

            _entered.TrySetResult(
                command.Transaction?.IsolationLevel);

            try
            {
                await _released.Task.WaitAsync(
                    TimeSpan.FromSeconds(45),
                    cancellationToken);

                return result;
            }
            catch
            {
                await result.DisposeAsync();

                throw;
            }
        }

        private bool IsOrganizationQuery(
            DbCommand command)
        {
            return command.CommandText
                       .TrimStart()
                       .StartsWith(
                           "SELECT",
                           StringComparison.OrdinalIgnoreCase)

                   && command.CommandText.Contains(
                       "organizations",
                       StringComparison.OrdinalIgnoreCase)

                   && command.Parameters
                       .Cast<DbParameter>()
                       .Any(parameter =>
                           parameter.Value is Guid id &&
                           id == organizationId);
        }
    }

    private sealed class OrganizationRemovalFailureInterceptor(
        Guid organizationId,
        Guid userId,
        Guid projectId) : SaveChangesInterceptor
    {
        public bool FailureInjected { get; private set; }

        public override async ValueTask<InterceptionResult<int>>
            SavingChangesAsync(
                DbContextEventData eventData,
                InterceptionResult<int> result,
                CancellationToken cancellationToken = default)
        {
            var context = eventData.Context;

            if (context is null)
                return result;

            var deletingOrganizationMember =
                context.ChangeTracker
                    .Entries<OrganizationMemberRecord>()
                    .Any(entry =>
                        entry.State == EntityState.Deleted &&
                        entry.Entity.OrganizationId ==
                            organizationId &&
                        entry.Entity.UserId == userId);

            if (!deletingOrganizationMember)
                return result;

            // ExecuteDeleteAsync must have already
            // removed the project membership within
            // the still-active database transaction.

            var projectMembershipExists =
                await context.Set<ProjectMemberRecord>()
                    .AnyAsync(
                        x => x.ProjectId == projectId &&
                             x.UserId == userId,
                        cancellationToken);

            Assert.False(projectMembershipExists);

            Assert.NotNull(
                context.Database.CurrentTransaction);

            FailureInjected = true;

            // Inject a known application failure so the
            // API returns its existing HTTP 409 response.

            throw new OrganizationRuleViolationException(
                "Simulated organization removal failure.");
        }
    }

    #endregion Test Interceptors
}
