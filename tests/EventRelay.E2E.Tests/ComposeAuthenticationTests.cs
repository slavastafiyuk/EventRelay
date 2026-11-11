using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Playwright;

namespace EventRelay.E2E.Tests;

public sealed class ComposeAuthenticationTests(
    ComposeStackFixture fixture)
    : IClassFixture<ComposeStackFixture>
{
    private const string RedirectUri = "http://localhost:5000/scalar/oauth-test";

    [Fact]
    [Trait("Category", "ComposeE2E")]
    public async Task PublicEndpoint_ThroughGateway_Returns200()
    {
        using var response =
            await fixture.Gateway.GetAsync("/api/projects/ping");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        Assert.Equal(
            "ControlPlaneService",
            json.RootElement.GetProperty("service").GetString());
    }

    // Test 2: Missing credentials must be rejected.
    [Fact]
    [Trait("Category", "ComposeE2E")]
    public async Task ProtectedEndpoint_WithoutJwt_Returns401()
    {
        using var response = await fixture.Gateway.GetAsync(
            $"/api/projects/{Guid.NewGuid()}");

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    // Test 3: The real service must reject an invalid JWT.
    [Fact]
    [Trait("Category", "ComposeE2E")]
    public async Task ProtectedEndpoint_InvalidJwt_Returns401()
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/projects/{Guid.NewGuid()}");

        request.Headers.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                "invalid.jwt.token");

        using var response =
            await fixture.Gateway.SendAsync(request);

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    // Test 4: Complete real authentication and API workflow.
    [Fact]
    [Trait("Category", "ComposeE2E")]
    public async Task RealKeycloakLogin_CanCreateAndReadProject()
    {
        var token = await LoginWithPkceAsync(fixture.Username, fixture.Password);

        var projectName = $"E2E-{Guid.NewGuid():N}";

        using var createRequest = new HttpRequestMessage(
            HttpMethod.Post,
            "/api/projects")
        {
            Content = JsonContent.Create(new
            {
                Name = projectName,
                Description = "Docker Compose authentication E2E test"
            })
        };

        createRequest.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        using var createResponse =
            await fixture.Gateway.SendAsync(createRequest);

        var createBody =
            await createResponse.Content.ReadAsStringAsync();

        Assert.True(
            createResponse.StatusCode == HttpStatusCode.Created,
            $"Expected 201, received " +
            $"{(int)createResponse.StatusCode}: {createBody}");

        using var createdJson = JsonDocument.Parse(createBody);

        var createdId = createdJson.RootElement
            .GetProperty("id")
            .GetGuid();

        Assert.NotEqual(Guid.Empty, createdId);

        // Step 3: Retrieve the project using the same JWT.
        using var getRequest = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/projects/{createdId}");

        getRequest.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        using var getResponse =
            await fixture.Gateway.SendAsync(getRequest);

        var getBody =
            await getResponse.Content.ReadAsStringAsync();

        Assert.True(
            getResponse.StatusCode == HttpStatusCode.OK,
            $"Expected 200, received " +
            $"{(int)getResponse.StatusCode}: {getBody}");

        using var projectJson = JsonDocument.Parse(getBody);

        var project = projectJson.RootElement;

        Assert.Equal(
            createdId,
            project.GetProperty("key")
                .GetProperty("id")
                .GetGuid());

        Assert.Equal(
            projectName,
            project.GetProperty("key")
                .GetProperty("name")
                .GetString());
    }


    [Fact]
    [Trait("Category", "ComposeE2E")]
    public async Task PrivateProject_OtherAuthenticatedUser_Returns403()
    {
        // Arrange: authenticate two distinct Keycloak users.
        var ownerToken = await LoginWithPkceAsync(
            fixture.Username,
            fixture.Password);

        var outsiderToken = await LoginWithPkceAsync(
            fixture.SecondUsername,
            fixture.SecondPassword);

        // Verify that Keycloak issued tokens for different identities.
        var tokenHandler = new JwtSecurityTokenHandler();

        var ownerSubject = tokenHandler
            .ReadJwtToken(ownerToken)
            .Subject;

        var outsiderSubject = tokenHandler
            .ReadJwtToken(outsiderToken)
            .Subject;

        Assert.False(string.IsNullOrWhiteSpace(ownerSubject));
        Assert.False(string.IsNullOrWhiteSpace(outsiderSubject));
        Assert.NotEqual(ownerSubject, outsiderSubject);

        // Create a new project owned by User A.
        var projectName = $"Authz-{Guid.NewGuid():N}";

        using var createRequest = new HttpRequestMessage(
            HttpMethod.Post,
            "/api/projects")
        {
            Content = JsonContent.Create(new
            {
                Name = projectName,
                Description = "Cross-user authorization E2E test"
            })
        };

        createRequest.Headers.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                ownerToken);

        using var createResponse =
            await fixture.Gateway.SendAsync(createRequest);

        var createBody =
            await createResponse.Content.ReadAsStringAsync();

        Assert.True(
            createResponse.StatusCode == HttpStatusCode.Created,
            $"Expected 201, received " +
            $"{(int)createResponse.StatusCode}: {createBody}");

        using var createdJson = JsonDocument.Parse(createBody);

        var projectId = createdJson.RootElement
            .GetProperty("id")
            .GetGuid();

        Assert.NotEqual(Guid.Empty, projectId);

        // Act 1: the owner reads the project.
        using var ownerRequest = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/projects/{projectId}");

        ownerRequest.Headers.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                ownerToken);

        using var ownerResponse =
            await fixture.Gateway.SendAsync(ownerRequest);

        // Assert: the owner is authorized.
        Assert.Equal(
            HttpStatusCode.OK,
            ownerResponse.StatusCode);

        // Act 2: a different authenticated user reads it.
        using var outsiderRequest = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/projects/{projectId}");

        outsiderRequest.Headers.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                outsiderToken);

        using var outsiderResponse =
            await fixture.Gateway.SendAsync(outsiderRequest);

        // Assert: a valid JWT is not sufficient for access.
        Assert.Equal(
            HttpStatusCode.Forbidden,
            outsiderResponse.StatusCode);

        // Act 3: an anonymous caller reads the same project.
        using var anonymousResponse = await fixture.Gateway.GetAsync(
            $"/api/projects/{projectId}");

        // Assert: missing authentication is a different failure.
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            anonymousResponse.StatusCode);
    }


    private async Task<string> LoginWithPkceAsync(string username, string password)
    {
        // Generate PKCE verifier and SHA-256 challenge.
        var verifier = Base64Url(
            RandomNumberGenerator.GetBytes(32));

        var challenge = Base64Url(
            SHA256.HashData(
                Encoding.ASCII.GetBytes(verifier)));

        var state = Guid.NewGuid().ToString("N");

        var realmUrl =
            $"{fixture.Keycloak.BaseAddress!.ToString().TrimEnd('/')}" +
            $"/realms/{ComposeStackFixture.Realm}";

        var authorizationUrl = QueryHelpers.AddQueryString(
            $"{realmUrl}/protocol/openid-connect/auth",
            new Dictionary<string, string?>
            {
                ["client_id"] = ComposeStackFixture.ClientId,
                ["response_type"] = "code",
                ["scope"] = "openid profile",
                ["redirect_uri"] = RedirectUri,
                ["code_challenge"] = challenge,
                ["code_challenge_method"] = "S256",
                ["state"] = state
            });

        // Log in through the real Keycloak browser UI.
        using var playwright = await Playwright.CreateAsync();

        await using var browser =
            await playwright.Chromium.LaunchAsync(
                new BrowserTypeLaunchOptions
                {
                    Headless = true
                });

        var page = await browser.NewPageAsync();

        await page.GotoAsync(authorizationUrl);
        await page.Locator("#username").FillAsync(username);
        await page.Locator("#password").FillAsync(password);

        // Capture the authorization-code redirect.
        var redirectTask = page.WaitForRequestAsync(
            request =>
                request.Url.StartsWith(
                    RedirectUri + "?",
                    StringComparison.OrdinalIgnoreCase)
                && request.Url.Contains("code="));

        await page.Locator("#kc-login").ClickAsync();

        var redirectRequest = await redirectTask;

        var query = QueryHelpers.ParseQuery(
            new Uri(redirectRequest.Url).Query);

        Assert.Equal(state, query["state"].ToString());

        var code = query["code"].ToString();

        Assert.False(string.IsNullOrWhiteSpace(code));

        // Exchange the code using the original PKCE verifier.
        using var tokenResponse = await fixture.Keycloak.PostAsync(
            $"/realms/{ComposeStackFixture.Realm}" +
            "/protocol/openid-connect/token",
            new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["grant_type"] = "authorization_code",
                    ["client_id"] = ComposeStackFixture.ClientId,
                    ["code"] = code,
                    ["redirect_uri"] = RedirectUri,
                    ["code_verifier"] = verifier
                }));

        Assert.True(
            tokenResponse.IsSuccessStatusCode,
            $"Keycloak token exchange failed: " +
            $"{(int)tokenResponse.StatusCode}");

        using var tokenJson = JsonDocument.Parse(
            await tokenResponse.Content.ReadAsStringAsync());

        var token = tokenJson.RootElement
            .GetProperty("access_token")
            .GetString();

        Assert.False(string.IsNullOrWhiteSpace(token));

        // Never write the access token to test logs.
        return token!;
    }


    [Fact]
    [Trait("Category", "ComposeE2E")]
    public async Task Owner_CanIssueAndRevokeApiKey_ThroughGateway()
    {
        var token = await LoginWithPkceAsync(
            fixture.Username,
            fixture.Password);

        // Create a project.
        using var createRequest = new HttpRequestMessage(
            HttpMethod.Post, "/api/projects")
        {
            Content = JsonContent.Create(new
            {
                Name = $"ApiKeys-{Guid.NewGuid():N}"
            })
        };

        createRequest.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        using var created = await fixture.Gateway.SendAsync(createRequest);

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        using var projectJson = JsonDocument.Parse(
            await created.Content.ReadAsStringAsync());

        var projectId = projectJson.RootElement
            .GetProperty("id").GetGuid();

        // Issue a project API key.
        using var issueRequest = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/projects/{projectId}/api-keys")
        {
            Content = JsonContent.Create(new
            {
                Name = "Gateway E2E key"
            })
        };

        issueRequest.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        using var issued = await fixture.Gateway.SendAsync(issueRequest);

        Assert.Equal(HttpStatusCode.Created, issued.StatusCode);

        using var keyJson = JsonDocument.Parse(
            await issued.Content.ReadAsStringAsync());

        var keyId = keyJson.RootElement
            .GetProperty("api")
            .GetProperty("key")
            .GetProperty("id")
            .GetGuid();

        var secret = keyJson.RootElement
            .GetProperty("secret")
            .GetString();

        Assert.StartsWith("er_", secret);

        // Revoke the key through the same Gateway.
        using var revokeRequest = new HttpRequestMessage(
            HttpMethod.Delete,
            $"/api/projects/{projectId}/api-keys/{keyId}");

        revokeRequest.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        using var revoked = await fixture.Gateway.SendAsync(revokeRequest);

        Assert.Equal(HttpStatusCode.NoContent, revoked.StatusCode);
    }


    private static string Base64Url(byte[] bytes)
    {
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}
