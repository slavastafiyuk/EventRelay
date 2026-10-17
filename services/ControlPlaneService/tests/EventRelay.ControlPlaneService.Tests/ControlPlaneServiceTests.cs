using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace EventRelay.ControlPlaneService.Tests;

public sealed class ControlPlaneServiceTests
    : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public ControlPlaneServiceTests(
        WebApplicationFactory<Program> factory)
    {
        var configuredFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:ProjectDatabase"] =
                            "Host=localhost;Database=unused;Username=test;Password=test"
                    });
            });
        });

        _client = configuredFactory.CreateClient();
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
            "ControlPlaneService",
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
