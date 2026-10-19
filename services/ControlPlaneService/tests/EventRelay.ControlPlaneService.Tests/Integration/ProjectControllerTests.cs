using System.Net;
using System.Net.Http.Json;
using EventRelay.ControlPlaneService.Infrastructure.Persistence;
using EventRelay.ControlPlaneService.Presentation.Contracts.Project;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace EventRelay.ControlPlaneService.Tests.Integration;

public sealed class ProjectControllerTests
{
    [Fact]
    public async Task Create_ThenGetById_ReturnsPersistedProject()
    {
        await using var postgres =
            new PostgreSqlBuilder("postgres:17-alpine")
                .Build();

        await postgres.StartAsync();
        var connectionString = postgres.GetConnectionString();

        var options =
            new DbContextOptionsBuilder<ProjectDbContext>()
                .UseNpgsql(connectionString)
                .Options;

        await using (var db = new ProjectDbContext(options))
        {
            await db.Database.MigrateAsync();
        }

        using var factory =
            new WebApplicationFactory<Program>()
                .WithWebHostBuilder(builder =>
                {
                    builder.ConfigureTestServices(services =>
                    {
                        services.ConfigureDbContext<ProjectDbContext>(
                            dbOptions => dbOptions.UseNpgsql(connectionString));
                    });
                });

        using var client = factory.CreateClient();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ProjectDbContext>();

            Assert.Equal(connectionString, db.Database.GetConnectionString());
        }

        var request = new ProjectCreateDTO
        {
            Name = "MyStore",
            Description = "Online store project"
        };

        using var createResponse = await client.PostAsJsonAsync("/api/projects", request);

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var created = await createResponse.Content.ReadFromJsonAsync<ProjectKeyDTO>();

        Assert.NotNull(created);
        Assert.NotEqual(Guid.Empty, created.Id);
        Assert.Equal("MyStore", created.Name);
        Assert.NotNull(createResponse.Headers.Location);

        using var getResponse = await client.GetAsync($"/api/projects/{created.Id}");

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var retrieved = await getResponse.Content.ReadFromJsonAsync<ProjectDTO>();

        Assert.NotNull(retrieved);
        Assert.Equal(created.Id, retrieved.Key.Id);
        Assert.Equal(created.Name, retrieved.Key.Name);
        Assert.Equal(request.Description, retrieved.Description);
    }
}
