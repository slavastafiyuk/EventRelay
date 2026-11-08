using DotNetEnv;

namespace EventRelay.E2E.Tests;

public sealed class ComposeStackFixture : IAsyncLifetime
{
    private const string GatewayUrl = "http://localhost:5000";
    private const string KeycloakUrl = "http://localhost:8081";

    public const string Realm = "eventrelay";
    public const string ClientId = "eventrelay-scalar";

    public string Username => "developer";
    public string Password { get; private set; } = null!;

    public string SecondUsername => "developer-b";
    public string SecondPassword { get; private set; } = null!;

    public HttpClient Gateway { get; } = new()
    {
        BaseAddress = new Uri(GatewayUrl),
        Timeout = TimeSpan.FromSeconds(15)
    };

    public HttpClient Keycloak { get; } = new()
    {
        BaseAddress = new Uri(KeycloakUrl),
        Timeout = TimeSpan.FromSeconds(15)
    };

    public async Task InitializeAsync()
    {
        LoadDevelopmentEnvironment();

        Password =
            Environment.GetEnvironmentVariable(
                "KEYCLOAK_DEV_USER_PASSWORD")
            ?? throw new InvalidOperationException(
                "KEYCLOAK_DEV_USER_PASSWORD is missing.");

        SecondPassword =
            Environment.GetEnvironmentVariable(
                "KEYCLOAK_SECOND_USER_PASSWORD")
            ?? throw new InvalidOperationException(
                "KEYCLOAK_SECOND_USER_PASSWORD is missing.");

        await WaitUntilAvailableAsync(
            Keycloak,
            $"/realms/{Realm}/.well-known/openid-configuration");

        await WaitUntilAvailableAsync(
            Gateway,
            "/api/projects/ping");
    }

    private static void LoadDevelopmentEnvironment()
    {
        var root = FindRepositoryRoot();
        var envPath = Path.Combine(root, ".env");

        if (File.Exists(envPath))
            Env.Load(envPath);
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory =
            new(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var composeFile = Path.Combine(
                directory.FullName,
                "docker-compose.yml");

            if (File.Exists(composeFile))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException(
            "Cannot locate EventRelay repository root.");
    }

    private static async Task WaitUntilAvailableAsync(
        HttpClient client,
        string path)
    {
        for (var attempt = 0; attempt < 45; attempt++)
        {
            try
            {
                using var response =
                    await client.GetAsync(path);

                if (response.IsSuccessStatusCode)
                    return;
            }
            catch (HttpRequestException)
            {
                // The service may still be starting.
            }
            catch (TaskCanceledException)
            {
                // Startup requests can time out.
            }

            await Task.Delay(TimeSpan.FromSeconds(2));
        }

        throw new InvalidOperationException(
            $"Docker Compose dependency is unavailable: " +
            $"{client.BaseAddress}{path.TrimStart('/')}");
    }

    public Task DisposeAsync()
    {
        Gateway.Dispose();
        Keycloak.Dispose();

        return Task.CompletedTask;
    }
}
