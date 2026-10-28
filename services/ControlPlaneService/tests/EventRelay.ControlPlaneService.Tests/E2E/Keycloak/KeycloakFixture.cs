using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Testcontainers.Keycloak;

namespace EventRelay.ControlPlaneService.Tests.E2E.Keycloak;

public sealed class KeycloakFixture : IAsyncLifetime
{
    public string Username { get; } = "developer";
    public string Password { get; } = "test-password";

    private const string AdminUsername = "admin";
    private const string AdminPassword = "admin";

    private readonly KeycloakContainer _keycloak =
        new KeycloakBuilder("quay.io/keycloak/keycloak:26.0")
            .WithUsername(AdminUsername)
            .WithPassword(AdminPassword)
            .Build();

    public string BaseUrl => _keycloak.GetBaseAddress();

    public async Task InitializeAsync()
    {
        await _keycloak.StartAsync();

        using var http = new HttpClient();

        // 1. Obtain an administrator access token.
        using var tokenResponse = await http.PostAsync(
            $"{BaseUrl.TrimEnd('/')}/realms/master/protocol/openid-connect/token",
            new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["grant_type"] = "password",
                    ["client_id"] = "admin-cli",
                    ["username"] = AdminUsername,
                    ["password"] = AdminPassword
                }));

        var tokenBody =
            await tokenResponse.Content.ReadAsStringAsync();

        Assert.True(
            tokenResponse.IsSuccessStatusCode,
            $"Failed to obtain Keycloak admin token. " +
            $"Status: {tokenResponse.StatusCode}. " +
            $"Response: {tokenBody}");

        using var tokenJson = JsonDocument.Parse(tokenBody);

        var accessToken = tokenJson.RootElement
            .GetProperty("access_token")
            .GetString();

        // 2. Read the existing realm configuration.
        var realmPath = Path.Combine(
            AppContext.BaseDirectory,
            "eventrelay-realm.json");

        var realmJson = await File.ReadAllTextAsync(realmPath);

        // 3. Replace the development password placeholder.
        realmJson = realmJson.Replace(
            "${KEYCLOAK_DEV_USER_PASSWORD}",
            Password,
            StringComparison.Ordinal);

        // 4. Import the realm through Keycloak's Admin API.
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"{BaseUrl.TrimEnd('/')}/admin/realms");

        request.Headers.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                accessToken);

        request.Content = new StringContent(
            realmJson,
            Encoding.UTF8,
            "application/json");

        using var response = await http.SendAsync(request);

        var responseBody =
            await response.Content.ReadAsStringAsync();

        Assert.True(
            response.StatusCode == HttpStatusCode.Created,
            $"Failed to import Keycloak realm. " +
            $"Status: {response.StatusCode}. " +
            $"Response: {responseBody}");

        // 5. Verify that the realm is available.
        var discoveryUrl =
            $"{BaseUrl.TrimEnd('/')}/realms/eventrelay/.well-known/openid-configuration";

        using var discoveryResponse =
            await http.GetAsync(discoveryUrl);

        Assert.True(
            discoveryResponse.IsSuccessStatusCode,
            $"Keycloak realm was imported but is unavailable. " +
            $"Status: {discoveryResponse.StatusCode}");
    }

    public async Task DisposeAsync()
    {
        await _keycloak.DisposeAsync();
    }
}
