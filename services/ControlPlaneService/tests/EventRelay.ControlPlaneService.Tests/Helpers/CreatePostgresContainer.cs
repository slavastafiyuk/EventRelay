using EventRelay.ControlPlaneService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace EventRelay.ControlPlaneService.Tests.Helpers;

public sealed class CreatePostgresContainer : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres =
        new PostgreSqlBuilder("postgres:17-alpine")
            .Build();
    private ApiFactory? _factory;

    public HttpClient Client { get; private set; } = null!;
    public string ConnectionString { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        ConnectionString = _postgres.GetConnectionString();

        var options = new DbContextOptionsBuilder<ControlPlaneDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        await using (var db = new ControlPlaneDbContext(options))
        {
            await RelationalDatabaseFacadeExtensions.MigrateAsync(db.Database);
        }

        _factory = new ApiFactory(ConnectionString);

        Client = _factory.CreateClient();

    }

    public async Task DisposeAsync()
    {
        Client?.Dispose();
        _factory?.Dispose();
        await _postgres.DisposeAsync();
    }

}
