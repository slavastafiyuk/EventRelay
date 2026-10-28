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

    private async Task<HttpResponseMessage> PostAsUserAsync(ProjectCreateDTO dto)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post, "/api/projects");

        request.Headers.Add("X-Test-User", "user-a");
        request.Content = JsonContent.Create(dto);

        return await _client.SendAsync(request);
    }

    private async Task<HttpResponseMessage> GetAsUserAsync(Guid projectId)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/projects/{projectId}");

        request.Headers.Add("X-Test-User", "user-a");

        return await _client.SendAsync(request);
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

        using var createResponse = await PostAsUserAsync(request);

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var created = await createResponse.Content.ReadFromJsonAsync<ProjectKeyDTO>();

        Assert.NotNull(created);
        Assert.NotEqual(Guid.Empty, created.Id);
        Assert.Equal(name, created.Name);
        Assert.NotNull(createResponse.Headers.Location);

        using var getResponse = await GetAsUserAsync(created.Id);

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var retrieved = await getResponse.Content.ReadFromJsonAsync<ProjectDTO>();

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

        using var firstResponse = await PostAsUserAsync(request);

        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);

        using var duplicateResponse = await PostAsUserAsync(request);

        Assert.Equal(HttpStatusCode.Conflict, duplicateResponse.StatusCode);

    }

    [Fact]
    public async Task GetById_UnknownProject_ReturnsNotFound()
    {
        using var response = await GetAsUserAsync(Guid.NewGuid());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Create_NameTooLong_ReturnsBadRequest()
    {
        var request = new ProjectCreateDTO
        {
            Name = new string('A', 151)
        };

        using var response = await PostAsUserAsync(request);

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

        using var response = await PostAsUserAsync(request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created = await response.Content.ReadFromJsonAsync<ProjectKeyDTO>();

        Assert.NotNull(created);

        var options = new DbContextOptionsBuilder<ControlPlaneDbContext>()
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

        using var response = await PostAsUserAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithoutAuthentication_Returns401()
    {
        var dto = new ProjectCreateDTO
        {
            Name = "UnauthorizedProject"
        };

        using var response = await _client.PostAsJsonAsync("/api/projects", dto);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetById_WithoutAuthentication_Returns401()
    {
        using var response = await _client.GetAsync($"/api/projects/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetById_AsDifferentUser_Returns403()
    {
        var dto = new ProjectCreateDTO
        {
            Name = $"Private-{Guid.NewGuid():N}"
        };

        using var createResponse = await PostAsUserAsync(dto);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var created = await createResponse.Content.ReadFromJsonAsync<ProjectKeyDTO>();
        Assert.NotNull(created);

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/projects/{created.Id}");

        request.Headers.Add("X-Test-User", "user-b");

        using var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
