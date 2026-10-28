
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EventRelay.Gateway.Tests.Integration;

public sealed record ForwardedRequest(
    string Service,
    string Method,
    string Path,
    string Authorization,
    string ApiKey);

public sealed class GatewayForwardingFixture : IAsyncLifetime
{
    private WebApplication? _controlPlane;
    private WebApplication? _eventService;
    private GatewayFactory? _gatewayFactory;

    public HttpClient Client { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        _controlPlane = await StartBackendAsync("controlplane");
        _eventService = await StartBackendAsync("events");

        var controlPlaneAddress = GetAddress(_controlPlane);
        var eventServiceAddress = GetAddress(_eventService);

        _gatewayFactory = new GatewayFactory(
            controlPlaneAddress,
            eventServiceAddress);

        Client = _gatewayFactory.CreateClient();
    }

    private static async Task<WebApplication> StartBackendAsync(
        string serviceName)
    {
        var builder = WebApplication.CreateBuilder();

        builder.WebHost.ConfigureKestrel(options =>
        {
            options.Listen(IPAddress.Loopback, 0);
        });

        var app = builder.Build();
        app.Map("/{**catchAll}", (HttpContext context) =>
        {
            var received = new ForwardedRequest(
                Service: serviceName,
                Method: context.Request.Method,
                Path: context.Request.Path.Value ?? "",
                Authorization:
                    context.Request.Headers.Authorization.ToString(),
                ApiKey:
                    context.Request.Headers["X-API-Key"].ToString());

            return Results.Json(received);
        });

        await app.StartAsync();

        return app;
    }

    private static string GetAddress(WebApplication app)
    {
        var server = app.Services.GetRequiredService<IServer>();

        var addresses = server.Features
            .Get<IServerAddressesFeature>()?.Addresses
            ?? throw new InvalidOperationException(
                "HTTP server addresses are unavailable.");

        return addresses.Single().TrimEnd('/') + "/";
    }

    public async Task DisposeAsync()
    {
        Client?.Dispose();
        _gatewayFactory?.Dispose();

        if (_controlPlane is not null)
            await _controlPlane.DisposeAsync();

        if (_eventService is not null)
            await _eventService.DisposeAsync();
    }

    private sealed class GatewayFactory(
        string controlPlaneAddress,
        string eventServiceAddress)
        : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(
            IWebHostBuilder builder)
        {
            builder.UseEnvironment("Production");
            builder.UseContentRoot(FindGatewayContentRoot());
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["ReverseProxy:Clusters:controlPlane:Destinations:primary:Address"]
                            = controlPlaneAddress,

                        ["ReverseProxy:Clusters:events:Destinations:primary:Address"]
                            = eventServiceAddress
                    });
            });
        }

        private static string FindGatewayContentRoot()
        {
            DirectoryInfo? directory =
                new(AppContext.BaseDirectory);

            while (directory is not null)
            {
                var candidate = Path.Combine(
                    directory.FullName,
                    "gateway",
                    "EventRelay.Gateway");

                if (File.Exists(
                    Path.Combine(candidate, "appsettings.json")))
                {
                    return candidate;
                }

                directory = directory.Parent;
            }

            throw new DirectoryNotFoundException(
                "Could not locate the Gateway project.");
        }
    }
}

public sealed class GatewayForwardingTests(
    GatewayForwardingFixture fixture)
    : IClassFixture<GatewayForwardingFixture>
{
    [Fact]
    public async Task ProjectRoute_ForwardsBearerToken()
    {
        const string token = "test-jwt-token";

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "/api/projects/ping");

        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        using var response =
            await fixture.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var received = await response.Content
            .ReadFromJsonAsync<ForwardedRequest>();

        Assert.NotNull(received);
        Assert.Equal("controlplane", received.Service);
        Assert.Equal($"Bearer {token}", received.Authorization);
        Assert.Equal("/api/projects/ping", received.Path);
    }

    [Fact]
    public async Task EventRoute_ForwardsApiKey()
    {
        const string apiKey = "test-api-key-123";

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "/api/events/ping");

        request.Headers.Add("X-API-Key", apiKey);

        using var response =
            await fixture.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var received = await response.Content
            .ReadFromJsonAsync<ForwardedRequest>();

        Assert.NotNull(received);
        Assert.Equal("events", received.Service);
        Assert.Equal(apiKey, received.ApiKey);
        Assert.Equal("/api/events/ping", received.Path);
    }

    [Theory]
    [InlineData("/api/projects/ping")]
    [InlineData("/api/events/ping")]
    public async Task Gateway_DoesNotCreateAuthenticationHeaders(
        string path)
    {
        using var response = await fixture.Client.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var received = await response.Content
            .ReadFromJsonAsync<ForwardedRequest>();

        Assert.NotNull(received);
        Assert.Equal(string.Empty, received.Authorization);
        Assert.Equal(string.Empty, received.ApiKey);
    }

    [Theory]
    [InlineData("/api/projects/ping", "controlplane")]
    [InlineData("/api/events/ping", "events")]
    public async Task Gateway_RoutesToCorrectDownstreamService(
        string path,
        string expectedService)
    {
        using var response = await fixture.Client.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var received = await response.Content
            .ReadFromJsonAsync<ForwardedRequest>();

        Assert.NotNull(received);
        Assert.Equal(expectedService, received.Service);
        Assert.Equal(path, received.Path);
    }
}
