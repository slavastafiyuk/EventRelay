using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace EventRelay.ProjectService.Tests;

public sealed class ProjectServiceTests
    : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public ProjectServiceTests(
        WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Ping_ReturnsExpectedResponse()
    {
        // Arrange
        const string url = "/api/projects/ping";

        // Act
        using var response = await _client.GetAsync(url);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();

        using var document = JsonDocument.Parse(json);

        var root = document.RootElement;

        Assert.Equal(
            "ProjectService",
            root.GetProperty("service").GetString());

        Assert.Equal(
            "ok",
            root.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Health_ReturnsOk()
    {
        // Arrange
        const string url = "/health";

        // Act
        using var response = await _client.GetAsync(url);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}