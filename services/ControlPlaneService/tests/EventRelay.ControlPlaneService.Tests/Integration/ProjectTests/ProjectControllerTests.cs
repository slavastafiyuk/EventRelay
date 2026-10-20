using System.Net;
using System.Net.Http.Json;
using EventRelay.ControlPlaneService.Infrastructure.Persistence;
using EventRelay.ControlPlaneService.Presentation.Contracts.Project;
using EventRelay.ControlPlaneService.Tests.Helpers;
using Microsoft.EntityFrameworkCore;

namespace EventRelay.ControlPlaneService.Tests.Integration.ProjectTests;

public sealed class ProjectControllerTests : IClassFixture<CreatePostgresContainer>
{
    private readonly HttpClient _client;
    private readonly CreatePostgresContainer _container;

    public ProjectControllerTests(CreatePostgresContainer container)
    {
        _container = container;
        _client = container.Client;
    }

    [Fact]
    public async Task Create_ThenGetById_ReturnsPersistedProject()
    {
        var name = $"MyStore-{Guid.NewGuid():N}";

        var request = new ProjectCreateDTO
        {
            Name = name,
            Description = "Online store project"
        };

        using var createResponse =
            await _client.PostAsJsonAsync("/api/projects", request);

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var created =
            await createResponse.Content.ReadFromJsonAsync<ProjectKeyDTO>();

        Assert.NotNull(created);
        Assert.NotEqual(Guid.Empty, created.Id);
        Assert.Equal(name, created.Name);
        Assert.NotNull(createResponse.Headers.Location);

        using var getResponse =
            await _client.GetAsync($"/api/projects/{created.Id}");

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var retrieved =
            await getResponse.Content.ReadFromJsonAsync<ProjectDTO>();

        Assert.NotNull(retrieved);
        Assert.Equal(created.Id, retrieved.Key.Id);
        Assert.Equal(name, retrieved.Key.Name);
        Assert.Equal(request.Description, retrieved.Description);

    }

    [Fact]
    public async Task Create_DuplicateName_ReturnsConflict()
    {

        var request = new ProjectCreateDTO
        {
            Name = $"Duplicate-{Guid.NewGuid():N}"
        };

        using var firstResponse =
            await _client.PostAsJsonAsync("/api/projects", request);

        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);

        using var duplicateResponse =
            await _client.PostAsJsonAsync("/api/projects", request);

        Assert.Equal(HttpStatusCode.Conflict, duplicateResponse.StatusCode);

    }

    [Fact]
    public async Task GetById_UnknownProject_ReturnsNotFound()
    {
        using var response =
            await _client.GetAsync($"/api/projects/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Create_NameTooLong_ReturnsBadRequest()
    {

        var request = new ProjectCreateDTO
        {
            Name = new string('A', 151)
        };

        using var response =
            await _client.PostAsJsonAsync("/api/projects", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }


    [Fact]
    public async Task Create_ProjectIsStoredInPostgres()
    {
        var name = $"Persisted-{Guid.NewGuid():N}";

        var request = new ProjectCreateDTO
        {
            Name = name,
            Description = "PostgreSQL integration test"
        };

        using var response = await _client.PostAsJsonAsync(
            "/api/projects",
            request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created =
            await response.Content.ReadFromJsonAsync<ProjectKeyDTO>();

        Assert.NotNull(created);

        var options =
            new DbContextOptionsBuilder<ControlPlaneDbContext>()
                .UseNpgsql(_container.ConnectionString)
                .Options;

        await using var db = new ControlPlaneDbContext(options);

        var persisted = await db.Projects
            .AsNoTracking()
            .SingleAsync(x => x.Id == created.Id);

        Assert.Equal(name, persisted.Name);
        Assert.Equal(request.Description, persisted.Description);
    }


    [Fact]
    public async Task Create_WhitespaceName_ReturnsBadRequest()
    {
        var request = new ProjectCreateDTO
        {
            Name = "   "
        };

        using var response = await _client.PostAsJsonAsync(
            "/api/projects",
            request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

}
