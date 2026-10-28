using System.Net;
using System.Text.Json;

namespace EventRelay.ControlPlaneService.Tests.Integration.OpenApi;

public sealed class OpenApiIntegrationTests
{
    private readonly HttpClient _client = new()
    {
        BaseAddress = new Uri("http://localhost:5000")
    };

    [Theory]
    [InlineData("/docs/specs/controlplane.json", "/api/projects")]
    [InlineData("/docs/specs/events.json", "/api/events/ping")]
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
        using var json = JsonDocument.Parse(
            await _client.GetStringAsync(
                "/docs/specs/controlplane.json"));

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
