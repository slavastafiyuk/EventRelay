using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using EventRelay.ControlPlaneService.Infrastructure.Persistence;
using EventRelay.ControlPlaneService.Presentation.Contracts.Organization;
using EventRelay.ControlPlaneService.Presentation.Contracts.Organization.Access;
using EventRelay.ControlPlaneService.Presentation.Contracts.Organization.Access.Actions;
using EventRelay.ControlPlaneService.Presentation.Contracts.Project;
using EventRelay.ControlPlaneService.Presentation.Contracts.User;
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

    private async Task<HttpResponseMessage> PostAsUserAsync(
    ProjectCreateDTO dto,
    string subject = "user-a")
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post, "/api/projects");

        request.Headers.Add("X-Test-User", subject);
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

        var organization = await OrganizationTestHelper.CreateAsync(_client, "user-a");

        var request = new ProjectCreateDTO
        {
            Name = name,
            Description = "Online store project",
            Organization = organization
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
        var organization = await OrganizationTestHelper.CreateAsync(_client, "user-a");

        var request = new ProjectCreateDTO
        {
            Name = $"Duplicate-{Guid.NewGuid():N}",
            Organization = organization
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
        var organization = await OrganizationTestHelper.CreateAsync(_client, "user-a");

        var request = new ProjectCreateDTO
        {
            Name = new string('A', 151),
            Organization = organization
        };

        using var response = await PostAsUserAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }


    [Fact]
    public async Task Create_ProjectIsStoredInPostgres()
    {
        var name = $"Persisted-{Guid.NewGuid():N}";

        var organization = await OrganizationTestHelper.CreateAsync(_client, "user-a");

        var request = new ProjectCreateDTO
        {
            Name = name,
            Description = "PostgreSQL integration test",
            Organization = organization
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
        Assert.Equal(organization.Id, persisted.OrganizationId);
    }


    [Fact]
    public async Task Create_WhitespaceName_ReturnsBadRequest()
    {
        var organization = await OrganizationTestHelper.CreateAsync(_client, "user-a");

        var request = new ProjectCreateDTO
        {
            Name = "   ",
            Organization = organization
        };

        using var response = await PostAsUserAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithoutAuthentication_Returns401()
    {
        var organization = await OrganizationTestHelper.CreateAsync(_client, "user-a");

        var dto = new ProjectCreateDTO
        {
            Name = "UnauthorizedProject",
            Organization = organization
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
        var organization = await OrganizationTestHelper.CreateAsync(_client, "user-a");

        var dto = new ProjectCreateDTO
        {
            Name = $"Private-{Guid.NewGuid():N}",
            Organization = organization
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
    [Fact]
    public async Task Create_AsNonOrganizationMember_Returns404()
    {
        var organization = await OrganizationTestHelper.CreateAsync(
            _client, "user-a");

        var dto = new ProjectCreateDTO
        {
            Name = $"Forbidden-{Guid.NewGuid():N}",
            Organization = organization
        };

        using var response = await PostAsUserAsync(dto, "user-b");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        await AssertProjectDoesNotExistAsync(dto.Name);
    }

    [Fact]
    public async Task Create_WithNonexistentOrganization_Returns404()
    {
        var dto = new ProjectCreateDTO
        {
            Name = $"MissingOrg-{Guid.NewGuid():N}",
            Organization = new OrganizationKeyDTO
            {
                Id = Guid.CreateVersion7(),
                Name = "Nonexistent organization"
            }
        };

        using var response = await PostAsUserAsync(dto);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        await AssertProjectDoesNotExistAsync(dto.Name);
    }

    [Fact]
    public async Task Create_WithEmptyOrganizationId_Returns400()
    {
        var dto = new ProjectCreateDTO
        {
            Name = $"InvalidOrg-{Guid.NewGuid():N}",
            Organization = new OrganizationKeyDTO
            {
                Id = Guid.Empty,
                Name = "Invalid organization"
            }
        };

        using var response = await PostAsUserAsync(dto);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        await AssertProjectDoesNotExistAsync(dto.Name);
    }

    private async Task AssertProjectDoesNotExistAsync(string name)
    {
        var options = new DbContextOptionsBuilder<ControlPlaneDbContext>()
            .UseNpgsql(_container.ConnectionString)
            .Options;

        await using var db = new ControlPlaneDbContext(options);

        Assert.False(await db.Projects
            .AnyAsync(x => x.Name == name));
    }

    [Fact]
    public async Task Create_WithoutOrganization_Returns400()
    {
        var name = $"MissingOrg-{Guid.NewGuid():N}";

        using var request = new HttpRequestMessage(
            HttpMethod.Post, "/api/projects");

        request.Headers.Add("X-Test-User", "user-a");

        request.Content = JsonContent.Create(new
        {
            Name = name
        });

        using var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        await AssertProjectDoesNotExistAsync(name);
    }


    [Theory]
    [InlineData(OrganizationRoleDTO.Member)]
    [InlineData(OrganizationRoleDTO.Admin)]
    public async Task Create_AsOrganizationMember_Returns201(
        OrganizationRoleDTO role)
    {
        var ownerSubject = $"owner-{Guid.NewGuid():N}";
        var memberSubject = $"member-{Guid.NewGuid():N}";

        var organization = await OrganizationTestHelper.CreateAsync(
            _client, ownerSubject);

        // Provision the second user.
        await OrganizationTestHelper.CreateAsync(
            _client, memberSubject);

        var options = new DbContextOptionsBuilder<ControlPlaneDbContext>()
            .UseNpgsql(_container.ConnectionString)
            .Options;

        await using var db = new ControlPlaneDbContext(options);

        var member = await db.Users
            .AsNoTracking()
            .SingleAsync(x => x.Subject == memberSubject);

        var action = new AddOrganizationMemberDTO
        {
            UserKey = new UserKeyDTO
            {
                Id = member.Id,
                Name = member.Name
            },
            Role = role
        };

        var jsonOptions = new JsonSerializerOptions(
            JsonSerializerDefaults.Web);

        jsonOptions.Converters.Add(new JsonStringEnumConverter());

        using var addRequest = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/organizations/{organization.Id}/members/actions");

        addRequest.Headers.Add("X-Test-User", ownerSubject);

        addRequest.Content =
            JsonContent.Create<OrganizationMemberActionDTO>(
                action, options: jsonOptions);

        using var addResponse = await _client.SendAsync(addRequest);

        Assert.Equal(
            HttpStatusCode.NoContent,
            addResponse.StatusCode);

        var dto = new ProjectCreateDTO
        {
            Name = $"MemberProject-{Guid.NewGuid():N}",
            Organization = organization
        };

        using var response = await PostAsUserAsync(
            dto, memberSubject);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created = await response.Content
            .ReadFromJsonAsync<ProjectKeyDTO>();

        Assert.NotNull(created);

        var persisted = await db.Projects
            .AsNoTracking()
            .SingleAsync(x => x.Id == created.Id);

        Assert.Equal(member.Id, persisted.OwnerId);
        Assert.Equal(organization.Id, persisted.OrganizationId);
    }

}
