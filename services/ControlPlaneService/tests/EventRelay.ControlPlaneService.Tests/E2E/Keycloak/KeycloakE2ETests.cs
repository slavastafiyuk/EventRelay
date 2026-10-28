using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using EventRelay.ControlPlaneService.Tests.Helpers;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Playwright;

namespace EventRelay.ControlPlaneService.Tests.E2E.Keycloak;

public sealed class KeycloakE2ETests(
    KeycloakFixture keycloak,
    CreatePostgresContainer postgres)
    : IClassFixture<KeycloakFixture>,
      IClassFixture<CreatePostgresContainer>
{
    private string Realm => $"{keycloak.BaseUrl.TrimEnd('/')}/realms/eventrelay";

    [Fact]
    [Trait("Category", "E2E")]
    public async Task PkceLogin_AllowsAuthenticatedApiRequest()
    {
        const string callback = "http://localhost:5000/scalar/oauth-test";
        const string clientId = "eventrelay-scalar";

        using var http = new HttpClient();

        // Generate PKCE parameters.
        var verifier = Base64Url(RandomNumberGenerator.GetBytes(32));
        var challenge = Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        var state = Guid.NewGuid().ToString("N");

        var authUrl = QueryHelpers.AddQueryString(
            $"{Realm}/protocol/openid-connect/auth",
            new Dictionary<string, string?>
            {
                ["client_id"] = clientId,
                ["response_type"] = "code",
                ["scope"] = "openid profile",
                ["redirect_uri"] = callback,
                ["code_challenge"] = challenge,
                ["code_challenge_method"] = "S256",
                ["state"] = state
            });

        // Authenticate through the real Keycloak login page.
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(
            new BrowserTypeLaunchOptions { Headless = true });

        var page = await browser.NewPageAsync();
        await page.GotoAsync(authUrl);

        await page.Locator("#username").FillAsync(keycloak.Username);
        await page.Locator("#password").FillAsync(keycloak.Password);

        var redirectTask = page.WaitForRequestAsync(
            request => request.Url.StartsWith(callback + "?") &&
                       request.Url.Contains("code="));

        await page.Locator("#kc-login").ClickAsync();

        var redirect = await redirectTask;
        var query = QueryHelpers.ParseQuery(new Uri(redirect.Url).Query);

        Assert.Equal(state, query["state"].ToString());

        var code = query["code"].ToString();
        Assert.False(string.IsNullOrWhiteSpace(code));

        // Exchange authorization code using the PKCE verifier.
        using var tokenResponse = await http.PostAsync(
            $"{Realm}/protocol/openid-connect/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["client_id"] = clientId,
                ["code"] = code,
                ["redirect_uri"] = callback,
                ["code_verifier"] = verifier
            }));

        var tokenBody = await tokenResponse.Content.ReadAsStringAsync();

        Assert.True(
            tokenResponse.IsSuccessStatusCode,
            $"Token exchange failed: {tokenResponse.StatusCode}");

        using var tokenJson = JsonDocument.Parse(tokenBody);
        var token = tokenJson.RootElement.GetProperty("access_token").GetString();

        Assert.False(string.IsNullOrWhiteSpace(token));

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        Assert.Equal(Realm, jwt.Issuer);
        Assert.Contains("eventrelay-api", jwt.Audiences);

        // Verify the token against the real API and PostgreSQL Testcontainer.
        await using var api = new AuthenticatedApiFixture(
            keycloak,
            postgres.ConnectionString);

        using var client = api.CreateClient();
        var endpoint = $"/api/projects/{Guid.NewGuid()}";

        using var unauthorized = await client.GetAsync(endpoint);
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);

        using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await client.SendAsync(request);
        var responseBody = await response.Content.ReadAsStringAsync();

        Assert.True(
            response.StatusCode == HttpStatusCode.NotFound,
            $"Expected 404, received {response.StatusCode}.\n{responseBody}");
    }

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
}
