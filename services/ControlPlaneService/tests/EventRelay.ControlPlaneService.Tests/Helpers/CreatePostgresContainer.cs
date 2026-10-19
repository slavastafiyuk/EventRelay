using EventRelay.ControlPlaneService.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace EventRelay.ControlPlaneService.Tests.Helpers;

public sealed class CreatePostgresContainer : IAsyncLifetime
{

    private readonly PostgreSqlContainer _postgres =
        new PostgreSqlBuilder("postgres:17-alpine")
            .Build();

    private WebApplicationFactory<Program>? _factory;

    public HttpClient Client { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        var connectionString = _postgres.GetConnectionString();

        var dbOptions =
            new DbContextOptionsBuilder<ProjectDbContext>()
                .UseNpgsql(connectionString)
                .Options;

        await using (var db = new ProjectDbContext(dbOptions))
        {
            await db.Database.MigrateAsync();
        }

        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.ConfigureDbContext<ProjectDbContext>(
                        options => options.UseNpgsql(connectionString));
                });
            });

        Client = _factory.CreateClient();
    }

    public async Task DisposeAsync()
    {
        Client?.Dispose();
        _factory?.Dispose();
        await _postgres.DisposeAsync();
    }

}
