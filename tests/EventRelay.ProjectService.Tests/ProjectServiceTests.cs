using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace EventRelay.ProjectService.Tests;

public class ProjectServiceTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public ProjectServiceTests(
        WebApplicationFactory<Program> factory
        )
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Ping_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/projects/ping");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("ProjectService", body);
    }
}