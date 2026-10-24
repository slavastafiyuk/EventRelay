
using System.Net;
using EventRelay.ControlPlaneService.Infrastructure.Persistence;
using EventRelay.ControlPlaneService.Tests.Helpers;
using Microsoft.EntityFrameworkCore;

namespace EventRelay.ControlPlaneService.Tests.Integration.UserTests;

public sealed class UserProvisioningTests(CreatePostgresContainer fixture) : IClassFixture<CreatePostgresContainer>
{
    private readonly HttpClient _client = fixture.Client;
    private const string Issuer = "http://localhost:8081/realms/eventrelay";

    private async Task<HttpResponseMessage> SendAsUserAsync(
        string subject,
        string path)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("X-Test-User", subject);

        return await _client.SendAsync(request);
    }

    private ControlPlaneDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ControlPlaneDbContext>()
            .UseNpgsql(fixture.ConnectionString)
            .Options;

        return new ControlPlaneDbContext(options);
    }

    [Fact]
    public async Task FirstAuthenticatedRequest_CreatesUser()
    {
        var subject = $"user-{Guid.NewGuid():N}";

        using var response = await SendAsUserAsync(subject, $"/api/projects/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        await using var db = CreateDbContext();
        var user = await db.Users
            .AsNoTracking()
            .SingleAsync(x =>
                x.Issuer == Issuer &&
                x.Subject == subject);

        Assert.NotEqual(Guid.Empty, user.Id);
        Assert.Equal(subject, user.Name);
    }

    [Fact]
    public async Task RepeatedAuthenticatedRequests_ReuseExistingUser()
    {
        var subject = $"user-{Guid.NewGuid():N}";

        using var firstResponse = await SendAsUserAsync(subject, $"/api/projects/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, firstResponse.StatusCode);

        Guid firstUserId;

        await using (var db = CreateDbContext())
        {
            firstUserId = await db.Users
                .AsNoTracking()
                .Where(x =>
                    x.Issuer == Issuer &&
                    x.Subject == subject)
                .Select(x => x.Id)
                .SingleAsync();
        }

        using var secondResponse = await SendAsUserAsync(subject, $"/api/projects/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, secondResponse.StatusCode);

        await using var verifyDb = CreateDbContext();

        var users = await verifyDb.Users
            .AsNoTracking()
            .Where(x =>
                x.Issuer == Issuer &&
                x.Subject == subject)
            .ToListAsync();

        var existingUser = Assert.Single(users);
        Assert.Equal(firstUserId, existingUser.Id);
    }

    [Fact]
    public async Task UnauthenticatedRequest_DoesNotCreateUser()
    {
        await using var db = CreateDbContext();
        var countBefore = await db.Users.CountAsync();

        using var response = await _client.GetAsync($"/api/projects/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var countAfter = await db.Users.CountAsync();
        Assert.Equal(countBefore, countAfter);
    }

    [Fact]
    public async Task AuthenticatedRequestToPublicEndpoint_DoesNotCreateUser()
    {
        var subject = $"user-{Guid.NewGuid():N}";

        using var response = await SendAsUserAsync(subject, "/api/projects/ping");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using var db = CreateDbContext();

        var exists = await db.Users
            .AsNoTracking()
            .AnyAsync(x =>
                x.Issuer == Issuer &&
                x.Subject == subject);

        Assert.False(exists);
    }
}
