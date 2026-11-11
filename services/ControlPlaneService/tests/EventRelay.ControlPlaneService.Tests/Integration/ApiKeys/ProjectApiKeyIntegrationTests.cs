
using System.Net;
using System.Net.Http.Json;
using EventRelay.ControlPlaneService.Domain.Entities.Api;
using EventRelay.ControlPlaneService.Infrastructure.Persistence;
using EventRelay.ControlPlaneService.Presentation.Contracts.Api;
using EventRelay.ControlPlaneService.Presentation.Contracts.Project;
using EventRelay.ControlPlaneService.Tests.Helpers;
using Microsoft.EntityFrameworkCore;

namespace EventRelay.ControlPlaneService.Tests.Integration.ApiKeys;

public sealed class ProjectApiKeyIntegrationTests(
    CreatePostgresContainer fixture)
    : IClassFixture<CreatePostgresContainer>
{
    private HttpClient Client => fixture.Client;

    private async Task<HttpResponseMessage> SendAsUserAsync(
        HttpMethod method,
        string path,
        string user,
        object? body = null)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Add("X-Test-User", user);

        if (body is not null)
            request.Content = JsonContent.Create(body);

        return await Client.SendAsync(request);
    }

    private async Task<(string User, Guid ProjectId)> CreateProjectAsync()
    {
        var user = $"owner-{Guid.NewGuid():N}";

        using var response = await SendAsUserAsync(
            HttpMethod.Post,
            "/api/projects",
            user,
            new ProjectCreateDTO
            {
                Name = $"Project-{Guid.NewGuid():N}"
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var project = await response.Content
            .ReadFromJsonAsync<ProjectKeyDTO>();

        return (user, Assert.IsType<ProjectKeyDTO>(project).Id);
    }

    private async Task<IssuedApiDTO> IssueAsync(
        string user,
        Guid projectId)
    {
        using var response = await SendAsUserAsync(
            HttpMethod.Post,
            $"/api/projects/{projectId}/api-keys",
            user,
            new CreateApiKeyDTO { Name = "Integration key" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore == true);

        var issued = await response.Content
            .ReadFromJsonAsync<IssuedApiDTO>();

        return Assert.IsType<IssuedApiDTO>(issued);
    }

    private async Task<HttpResponseMessage> ResolveAsync(
        string hash,
        string? internalToken)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "/internal/api-keys/resolve")
        {
            Content = JsonContent.Create(
                new ResolveApiKeyDTO { KeyHash = hash })
        };

        if (internalToken is not null)
        {
            request.Headers.Add(
                "X-Internal-Token", internalToken);
        }

        return await Client.SendAsync(request);
    }

    [Fact]
    public async Task Issue_StoresHash_AndResolvesProject()
    {
        var (user, projectId) = await CreateProjectAsync();
        var issued = await IssueAsync(user, projectId);

        Assert.StartsWith("er_", issued.Secret);
        Assert.Equal(projectId, issued.Api.Project.Id);
        Assert.True(issued.Api.IsActive);

        // Verify the raw secret was not stored.
        var options = new DbContextOptionsBuilder<ControlPlaneDbContext>()
            .UseNpgsql(fixture.ConnectionString)
            .Options;

        await using (var db = new ControlPlaneDbContext(options))
        {
            var record = await db.ProjectApiKeys
                .AsNoTracking()
                .SingleAsync(x => x.Id == issued.Api.Key.Id);

            Assert.Equal(ApiKey.Hash(issued.Secret), record.KeyHash);
            Assert.NotEqual(issued.Secret, record.KeyHash);
        }

        // Verify the internal resolution endpoint.
        using var response = await ResolveAsync(
            ApiKey.Hash(issued.Secret),
            ApiFactory.InternalServiceTokenForTests);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore == true);

        var resolved = await response.Content
            .ReadFromJsonAsync<ResolvedApiKeyDTO>();

        Assert.Equal(
            projectId,
            Assert.IsType<ResolvedApiKeyDTO>(resolved).ProjectId);
    }

    [Fact]
    public async Task InternalResolve_RequiresCorrectToken()
    {
        var unknownHash = new string('A', 64);

        using var missing = await ResolveAsync(unknownHash, null);
        Assert.Equal(HttpStatusCode.Unauthorized, missing.StatusCode);

        using var wrong = await ResolveAsync(
            unknownHash, "wrong-internal-token");

        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);

        using var valid = await ResolveAsync(
            unknownHash,
            ApiFactory.InternalServiceTokenForTests);

        // Authenticated, but the key does not exist.
        Assert.Equal(HttpStatusCode.NotFound, valid.StatusCode);
    }

    [Fact]
    public async Task Revoke_IsIdempotent_AndDisablesResolution()
    {
        var (user, projectId) = await CreateProjectAsync();
        var issued = await IssueAsync(user, projectId);

        var path =
            $"/api/projects/{projectId}/api-keys/{issued.Api.Key.Id}";

        using var first = await SendAsUserAsync(
            HttpMethod.Delete, path, user);

        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);

        // Repeating revocation must be safe.
        using var second = await SendAsUserAsync(
            HttpMethod.Delete, path, user);

        Assert.Equal(HttpStatusCode.NoContent, second.StatusCode);

        using var resolve = await ResolveAsync(
            ApiKey.Hash(issued.Secret),
            ApiFactory.InternalServiceTokenForTests);

        Assert.Equal(HttpStatusCode.NotFound, resolve.StatusCode);
    }

    [Fact]
    public async Task DifferentUser_CannotManageProjectApiKeys()
    {
        var (owner, projectId) = await CreateProjectAsync();
        var issued = await IssueAsync(owner, projectId);

        var outsider = $"outsider-{Guid.NewGuid():N}";

        using var issueResponse = await SendAsUserAsync(
            HttpMethod.Post,
            $"/api/projects/{projectId}/api-keys",
            outsider,
            new CreateApiKeyDTO { Name = "Unauthorized key" });

        Assert.Equal(HttpStatusCode.Forbidden, issueResponse.StatusCode);

        using var revokeResponse = await SendAsUserAsync(
            HttpMethod.Delete,
            $"/api/projects/{projectId}/api-keys/{issued.Api.Key.Id}",
            outsider);

        Assert.Equal(HttpStatusCode.Forbidden, revokeResponse.StatusCode);

        // Unauthorized revocation must not disable the key.
        using var resolve = await ResolveAsync(
            ApiKey.Hash(issued.Secret),
            ApiFactory.InternalServiceTokenForTests);

        Assert.Equal(HttpStatusCode.OK, resolve.StatusCode);
    }

    [Fact]
    public async Task Issue_RejectsMissingAuthenticationAndInvalidName()
    {
        var (user, projectId) = await CreateProjectAsync();
        var path = $"/api/projects/{projectId}/api-keys";

        using var anonymous = await Client.PostAsJsonAsync(
            path,
            new CreateApiKeyDTO { Name = "Anonymous" });

        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

        using var invalid = await SendAsUserAsync(
            HttpMethod.Post,
            path,
            user,
            new CreateApiKeyDTO { Name = "   " });

        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }
}
