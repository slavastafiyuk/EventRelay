
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using EventRelay.ControlPlaneService.Domain.Entities.Project;
using EventRelay.ControlPlaneService.Domain.Entities.Project.Access;
using EventRelay.ControlPlaneService.Domain.Entities.User;
using EventRelay.ControlPlaneService.Infrastructure.Persistence;
using EventRelay.ControlPlaneService.Infrastructure.Persistence.Models.Project.Access;
using EventRelay.ControlPlaneService.Infrastructure.Persistence.Models.User;
using EventRelay.ControlPlaneService.Presentation.Contracts.Project;
using EventRelay.ControlPlaneService.Presentation.Contracts.Project.Access;
using EventRelay.ControlPlaneService.Repository.Repositories;
using EventRelay.ControlPlaneService.Tests.Helpers;
using Microsoft.EntityFrameworkCore;

namespace EventRelay.ControlPlaneService.Tests.Integration.ProjectTests;

public sealed class ProjectMemberIntegrationTests(CreatePostgresContainer fixture)
    : IClassFixture<CreatePostgresContainer>
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private ControlPlaneDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ControlPlaneDbContext>()
            .UseNpgsql(fixture.ConnectionString)
            .Options;

        return new ControlPlaneDbContext(options);
    }

    private static UserRecord UserRecordFor(
        UserKey key,
        string subject) => new()
        {
            Id = key.Id,
            Name = key.Name,
            Issuer = "integration-test",
            Subject = subject
        };

    private async Task<HttpResponseMessage> SendAsAsync(
        HttpMethod method,
        string path,
        string subject,
        object? body = null)
    {
        using var request = new HttpRequestMessage(method, path);

        request.Headers.Add("X-Test-User", subject);

        if (body is not null)
            request.Content = JsonContent.Create(body);

        return await fixture.Client.SendAsync(request);
    }

    private async Task<Guid> CreateProjectAsAsync(string subject)
    {
        using var response = await SendAsAsync(
            HttpMethod.Post,
            "/api/projects",
            subject,
            new ProjectCreateDTO
            {
                Name = $"Members-{Guid.NewGuid():N}"
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var project = await response.Content
            .ReadFromJsonAsync<ProjectKeyDTO>();

        Assert.NotNull(project);

        return project.Id;
    }

    // Member-management HTTP endpoints do not exist yet.
    // Seed membership using EF Core, then verify the real GET API.
    private async Task<Guid> AddInternalMemberInDatabaseAsync(
        Guid projectId,
        string subject,
        ProjectRole role)
    {
        // An authenticated request provisions the test user.
        // Before membership, the private project is inaccessible.
        using (var beforeMembership = await SendAsAsync(
            HttpMethod.Get,
            $"/api/projects/{projectId}",
            subject))
        {
            Assert.Equal(
                HttpStatusCode.Forbidden,
                beforeMembership.StatusCode);
        }

        await using var db = CreateDbContext();

        var member = await db.Users
            .SingleAsync(u => u.Subject == subject);

        var project = await db.Projects
            .Include(p => p.Access)
            .SingleAsync(p => p.Id == projectId);

        project.Access.Visibility = ProjectVisibility.Internal;

        db.Set<ProjectMemberRecord>().Add(new ProjectMemberRecord
        {
            ProjectId = projectId,
            UserId = member.Id,
            Role = role
        });

        await db.SaveChangesAsync();

        return member.Id;
    }

    [Fact]
    public async Task ProjectWithMembers_SaveAndReload_PreservesMembersAndRoles()
    {
        var owner = new UserKey(
            Guid.CreateVersion7(),
            "test-owner");

        var member = new UserKey(
            Guid.CreateVersion7(),
            "test-editor");

        var project = new Project(
            new ProjectKey($"MemberRoundTrip-{Guid.NewGuid():N}"),
            owner);

        project.ChangeVisibility(ProjectVisibility.Internal);
        project.AddMember(member, ProjectRole.Editor);

        // Arrange: persist the project and users in PostgreSQL.
        await using (var db = CreateDbContext())
        {
            db.Users.Add(UserRecordFor(
                owner,
                $"owner-{Guid.NewGuid():N}"));

            db.Users.Add(UserRecordFor(
                member,
                $"member-{Guid.NewGuid():N}"));

            await db.SaveChangesAsync();

            var repository = new ProjectRepository(db);

            await repository.AddAsync(
                project,
                CancellationToken.None);

            await db.SaveChangesAsync();
        }

        // Act: use a new DbContext to reload database state.
        await using (var db = CreateDbContext())
        {
            var storedMember = await db.Set<ProjectMemberRecord>()
                .AsNoTracking()
                .SingleAsync(m =>
                    m.ProjectId == project.Key.Id &&
                    m.UserId == member.Id);

            Assert.Equal(ProjectRole.Editor, storedMember.Role);

            var repository = new ProjectRepository(db);

            var reloaded = await repository.GetByIdAsync(
                project.Key.Id,
                CancellationToken.None);

            // Assert: database -> repository -> domain model.
            Assert.NotNull(reloaded);

            Assert.Equal(
                ProjectVisibility.Internal,
                reloaded.Access.Visibility);

            var actualMember = Assert.Single(
                reloaded.Access.Members);

            Assert.Equal(member.Id, actualMember.User.Id);
            Assert.Equal(member.Name, actualMember.User.Name);
            Assert.Equal(ProjectRole.Editor, actualMember.Role);
        }
    }

    [Fact]
    public async Task GetById_InternalProject_ReturnsPersistedMembers()
    {
        var ownerSubject = $"owner-{Guid.NewGuid():N}";
        var memberSubject = $"member-{Guid.NewGuid():N}";

        var projectId = await CreateProjectAsAsync(ownerSubject);

        var memberId = await AddInternalMemberInDatabaseAsync(
            projectId,
            memberSubject,
            ProjectRole.Editor);

        // Act: GET through the real ASP.NET Core pipeline.
        using var response = await SendAsAsync(
            HttpMethod.Get,
            $"/api/projects/{projectId}",
            ownerSubject);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var retrieved = await response.Content
            .ReadFromJsonAsync<ProjectDTO>(JsonOptions);

        Assert.NotNull(retrieved);

        Assert.Equal(
            ProjectVisibilityDTO.Internal,
            retrieved.Access.Visibility);

        var apiMember = Assert.Single(retrieved.Access.Members);

        Assert.Equal(memberId, apiMember.User.Id);
        Assert.Equal(ProjectRoleDTO.Editor, apiMember.Role);

        // A member of an internal project can read it.
        using var memberResponse = await SendAsAsync(
            HttpMethod.Get,
            $"/api/projects/{projectId}",
            memberSubject);

        Assert.Equal(
            HttpStatusCode.OK,
            memberResponse.StatusCode);
    }

    [Fact]
    public async Task GetById_RemovedMember_CannotReadInternalProject()
    {
        var ownerSubject = $"owner-{Guid.NewGuid():N}";
        var memberSubject = $"member-{Guid.NewGuid():N}";

        var projectId = await CreateProjectAsAsync(ownerSubject);

        var memberId = await AddInternalMemberInDatabaseAsync(
            projectId,
            memberSubject,
            ProjectRole.Viewer);

        // Before removal: member has access.
        using (var beforeRemoval = await SendAsAsync(
            HttpMethod.Get,
            $"/api/projects/{projectId}",
            memberSubject))
        {
            Assert.Equal(
                HttpStatusCode.OK,
                beforeRemoval.StatusCode);
        }

        // Remove membership directly from PostgreSQL.
        await using (var db = CreateDbContext())
        {
            var member = await db.Set<ProjectMemberRecord>()
                .SingleAsync(m =>
                    m.ProjectId == projectId &&
                    m.UserId == memberId);

            db.Remove(member);

            await db.SaveChangesAsync();
        }

        // After removal: member must not access the project.
        using var afterRemoval = await SendAsAsync(
            HttpMethod.Get,
            $"/api/projects/{projectId}",
            memberSubject);

        Assert.Equal(
            HttpStatusCode.Forbidden,
            afterRemoval.StatusCode);
    }
}
