
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Playwright;

namespace EventRelay.ControlPlaneService.Tests.E2E.Keycloak;

public sealed class KeycloakE2ETests
{
    private const string Realm = "http://localhost:8081/realms/eventrelay";
    private const string Callback = "http://localhost:5000/scalar/oauth-test";
    private const string Gateway = "http://localhost:5000";

    [Fact]
    [Trait("Category", "E2E")]
    public async Task PkceLogin_AllowsAuthenticatedGatewayRequest()
    {
        var password = GetPassword();

        var verifier = Base64Url(RandomNumberGenerator.GetBytes(32));
        var challenge = Base64Url(
            SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        var state = Guid.NewGuid().ToString("N");

        var authUrl = QueryHelpers.AddQueryString(
            $"{Realm}/protocol/openid-connect/auth",
            new Dictionary<string, string?>
            {
                ["client_id"] = "eventrelay-scalar",
                ["response_type"] = "code",
                ["scope"] = "openid profile",
                ["redirect_uri"] = Callback,
                ["code_challenge"] = challenge,
                ["code_challenge_method"] = "S256",
                ["state"] = state
            });

        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync();
        var page = await browser.NewPageAsync();

        await page.GotoAsync(authUrl);
        await page.Locator("#username").FillAsync("developer");
        await page.Locator("#password").FillAsync(password);
        await page.Locator("#kc-login").ClickAsync();

        await page.WaitForURLAsync(url =>
            url.StartsWith(Callback + "?") && url.Contains("code="));

        var query = QueryHelpers.ParseQuery(new Uri(page.Url).Query);
        Assert.Equal(state, query["state"].ToString());

        var code = query["code"].ToString();
        Assert.False(string.IsNullOrEmpty(code));

        using var http = new HttpClient();

        using var tokenResponse = await http.PostAsync(
            $"{Realm}/protocol/openid-connect/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["client_id"] = "eventrelay-scalar",
                ["code"] = code,
                ["redirect_uri"] = Callback,
                ["code_verifier"] = verifier
            }));

        tokenResponse.EnsureSuccessStatusCode();

        using var json = JsonDocument.Parse(
            await tokenResponse.Content.ReadAsStringAsync());

        var token = json.RootElement
            .GetProperty("access_token").GetString();

        Assert.False(string.IsNullOrWhiteSpace(token));

        var endpoint = $"{Gateway}/api/projects/{Guid.NewGuid()}";

        using var unauthorized = await http.GetAsync(endpoint);
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);

        using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        using var response = await http.SendAsync(request);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static string GetPassword()
    {
        var password = Environment.GetEnvironmentVariable(
            "KEYCLOAK_DEV_USER_PASSWORD");

        if (!string.IsNullOrWhiteSpace(password))
            return password;

        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null &&
               !File.Exists(Path.Combine(directory.FullName, ".env")))
            directory = directory.Parent;

        if (directory is null)
            throw new FileNotFoundException(".env file not found.");

        DotNetEnv.Env.Load(Path.Combine(directory.FullName, ".env"));

        return Environment.GetEnvironmentVariable(
            "KEYCLOAK_DEV_USER_PASSWORD")
            ?? throw new InvalidOperationException(
                "Keycloak development password is missing.");
    }

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
}
