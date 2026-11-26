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
    private const string RedirectUri =
        "http://localhost:5000/scalar/oauth-test";

    private sealed record OrganizationKey(
        Guid Id,
        string Name);

    #region Tests

    [Fact]
    [Trait("Category", "ComposeE2E")]
    public async Task PublicEndpoint_ThroughGateway_Returns200()
    {
        using var response =
            await fixture.Gateway.GetAsync("/api/projects/ping");

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        using var json = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        Assert.Equal(
            "ControlPlaneService",
            json.RootElement
                .GetProperty("service")
                .GetString());
    }

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

    [Fact]
    [Trait("Category", "ComposeE2E")]
    public async Task RealKeycloakLogin_CanCreateAndReadProject()
    {
        var token = await LoginWithPkceAsync(
            fixture.Username,
            fixture.Password);

        var organization = await CreateOrganizationAsync(token);

        var projectName = $"E2E-{Guid.NewGuid():N}";

        var projectId = await CreateProjectAsync(
            token,
            organization,
            projectName,
            "Docker Compose authentication E2E test");

        using var getRequest = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/projects/{projectId}");

        getRequest.Headers.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                token);

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
            projectId,
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
        var ownerToken = await LoginWithPkceAsync(
            fixture.Username,
            fixture.Password);

        var outsiderToken = await LoginWithPkceAsync(
            fixture.SecondUsername,
            fixture.SecondPassword);

        var tokenHandler = new JwtSecurityTokenHandler();

        var ownerSubject = tokenHandler
            .ReadJwtToken(ownerToken)
            .Subject;

        var outsiderSubject = tokenHandler
            .ReadJwtToken(outsiderToken)
            .Subject;

        Assert.False(
            string.IsNullOrWhiteSpace(ownerSubject));

        Assert.False(
            string.IsNullOrWhiteSpace(outsiderSubject));

        Assert.NotEqual(
            ownerSubject,
            outsiderSubject);

        var organization =
            await CreateOrganizationAsync(ownerToken);

        var projectId = await CreateProjectAsync(
            ownerToken,
            organization,
            $"Authz-{Guid.NewGuid():N}",
            "Cross-user authorization E2E test");

        // The owner can access the project.

        using var ownerRequest = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/projects/{projectId}");

        ownerRequest.Headers.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                ownerToken);

        using var ownerResponse =
            await fixture.Gateway.SendAsync(ownerRequest);

        Assert.Equal(
            HttpStatusCode.OK,
            ownerResponse.StatusCode);

        // An authenticated outsider cannot access it.

        using var outsiderRequest = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/projects/{projectId}");

        outsiderRequest.Headers.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                outsiderToken);

        using var outsiderResponse =
            await fixture.Gateway.SendAsync(outsiderRequest);

        Assert.Equal(
            HttpStatusCode.Forbidden,
            outsiderResponse.StatusCode);

        // Anonymous requests must be rejected.

        using var anonymousResponse =
            await fixture.Gateway.GetAsync(
                $"/api/projects/{projectId}");

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            anonymousResponse.StatusCode);
    }

    [Fact]
    [Trait("Category", "ComposeE2E")]
    public async Task Owner_CanIssueAndRevokeApiKey_ThroughGateway()
    {
        var token = await LoginWithPkceAsync(
            fixture.Username,
            fixture.Password);

        var organization =
            await CreateOrganizationAsync(token);

        var projectId = await CreateProjectAsync(
            token,
            organization,
            $"ApiKeys-{Guid.NewGuid():N}");

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
            new AuthenticationHeaderValue(
                "Bearer",
                token);

        using var issued =
            await fixture.Gateway.SendAsync(issueRequest);

        Assert.Equal(
            HttpStatusCode.Created,
            issued.StatusCode);

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

        // Revoke the API key through the same Gateway.

        using var revokeRequest = new HttpRequestMessage(
            HttpMethod.Delete,
            $"/api/projects/{projectId}/api-keys/{keyId}");

        revokeRequest.Headers.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                token);

        using var revoked =
            await fixture.Gateway.SendAsync(revokeRequest);

        Assert.Equal(
            HttpStatusCode.NoContent,
            revoked.StatusCode);
    }

    #endregion Tests

    #region Organization and Project Helpers

    private async Task<OrganizationKey> CreateOrganizationAsync(
        string token)
    {
        var organizationName =
            $"Organization-{Guid.NewGuid():N}";

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "/api/organizations")
        {
            Content = JsonContent.Create(new
            {
                Name = organizationName
            })
        };

        request.Headers.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                token);

        using var response =
            await fixture.Gateway.SendAsync(request);

        var body =
            await response.Content.ReadAsStringAsync();

        Assert.True(
            response.StatusCode == HttpStatusCode.Created,
            $"Expected organization creation to return 201, " +
            $"received {(int)response.StatusCode}: {body}");

        using var json = JsonDocument.Parse(body);

        var organizationId = json.RootElement
            .GetProperty("id")
            .GetGuid();

        var returnedName = json.RootElement
            .GetProperty("name")
            .GetString();

        Assert.NotEqual(
            Guid.Empty,
            organizationId);

        Assert.Equal(
            organizationName,
            returnedName);

        return new OrganizationKey(
            organizationId,
            returnedName!);
    }

    private async Task<Guid> CreateProjectAsync(
        string token,
        OrganizationKey organization,
        string name,
        string? description = null)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "/api/projects")
        {
            Content = JsonContent.Create(new
            {
                Name = name,
                Description = description,

                Organization = new
                {
                    organization.Id,
                    organization.Name
                }
            })
        };

        request.Headers.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                token);

        using var response =
            await fixture.Gateway.SendAsync(request);

        var body =
            await response.Content.ReadAsStringAsync();

        Assert.True(
            response.StatusCode == HttpStatusCode.Created,
            $"Expected project creation to return 201, " +
            $"received {(int)response.StatusCode}: {body}");

        using var json = JsonDocument.Parse(body);

        var projectId = json.RootElement
            .GetProperty("id")
            .GetGuid();

        Assert.NotEqual(
            Guid.Empty,
            projectId);

        Assert.Equal(
            name,
            json.RootElement
                .GetProperty("name")
                .GetString());

        return projectId;
    }

    #endregion Organization and Project Helpers

    #region Authentication Helpers

    private async Task<string> LoginWithPkceAsync(
        string username,
        string password)
    {
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

        using var playwright =
            await Playwright.CreateAsync();

        await using var browser =
            await playwright.Chromium.LaunchAsync(
                new BrowserTypeLaunchOptions
                {
                    Headless = true
                });

        var page = await browser.NewPageAsync();

        await page.GotoAsync(authorizationUrl);

        await page.Locator("#username")
            .FillAsync(username);

        await page.Locator("#password")
            .FillAsync(password);

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

        Assert.Equal(
            state,
            query["state"].ToString());

        var code = query["code"].ToString();

        Assert.False(
            string.IsNullOrWhiteSpace(code));

        using var tokenResponse =
            await fixture.Keycloak.PostAsync(
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

        Assert.False(
            string.IsNullOrWhiteSpace(token));

        return token!;
    }

    private static string Base64Url(byte[] bytes)
    {
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    #endregion Authentication Helpers
}
