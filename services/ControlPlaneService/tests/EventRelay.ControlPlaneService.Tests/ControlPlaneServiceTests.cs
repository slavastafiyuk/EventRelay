using System.Net;
using System.Text.Json;
using EventRelay.ControlPlaneService.Tests.Helpers;

namespace EventRelay.ControlPlaneService.Tests;

public sealed class ControlPlaneServiceTests : IDisposable
{
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public ControlPlaneServiceTests()
    {
        _factory = new ApiFactory("Host=localhost;Database=unused;Username=test;Password=test");
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task Ping_ReturnsExpectedResponse()
    {
        const string url = "/api/projects/ping";
        using var response = await _client.GetAsync(url);

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
        const string url = "/health";
        using var response = await _client.GetAsync(url);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }
}
