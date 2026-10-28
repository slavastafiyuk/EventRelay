using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace EventRelay.ControlPlaneService.Tests.Integration.OpenApi;

public sealed class OpenApiIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public OpenApiIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task OpenApiDocument_ShouldBeAvailable()
    {
        var response = await _client.GetAsync("/openapi/v1.json");
        response.EnsureSuccessStatusCode();
    }

    [Theory]
    [InlineData("/openapi/v1.json", "/api/projects")]
    public async Task OpenApi_ReturnsValidDocument(string url, string endpoint)
    {
        using var response = await _client.GetAsync(url);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        var root = json.RootElement;

        Assert.StartsWith(
            "3.", root.GetProperty("openapi").GetString());

        Assert.True(
            root.GetProperty("paths")
                .TryGetProperty(endpoint, out _));
    }

    [Fact]
    public async Task ProtectedEndpoints_RequireOAuth2()
    {
        using var json = JsonDocument.Parse(await _client.GetStringAsync("/openapi/v1.json"));

        var root = json.RootElement;

        var scheme = root.GetProperty("components")
            .GetProperty("securitySchemes")
            .GetProperty("OAuth2");

        Assert.Equal("oauth2",
            scheme.GetProperty("type").GetString());

        Assert.True(
            scheme.GetProperty("flows")
                .TryGetProperty("authorizationCode", out _));

        var paths = root.GetProperty("paths");

        foreach (var operation in new[]
        {
            paths.GetProperty("/api/projects").GetProperty("post"),
            paths.GetProperty("/api/projects/{id}").GetProperty("get")
        })
        {
            Assert.Contains(
                operation.GetProperty("security").EnumerateArray(),
                requirement => requirement.TryGetProperty(
                    "OAuth2", out _));
        }
    }
}
