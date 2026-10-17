using EventRelay.ControlPlaneService.Domain.Entities.Project;
using EventRelay.ControlPlaneService.Infrastructure.Persistence;
using EventRelay.ControlPlaneService.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace EventRelay.ControlPlaneService.Tests.Integration;

public sealed class ProjectPersistenceTests
{
    [Fact]
    public async Task Project_CanBeSavedAndLoaded()
    {
        await using var postgres =
            new PostgreSqlBuilder("postgres:17-alpine")
                .Build();

        await postgres.StartAsync();

        var options =
            new DbContextOptionsBuilder<ProjectDbContext>()
                .UseNpgsql(postgres.GetConnectionString())
                .Options;

        await using (var db = new ProjectDbContext(options))
        {
            await db.Database.MigrateAsync();
        }

        var original = new Project(
            new ProjectKey("MyStore"))
        {
            Description = "Online shop"
        };

        var record = new ProjectRecord
        {
            Id = original.Key.Id,
            Name = original.Key.Name,
            Description = original.Description
        };

        await using (var db = new ProjectDbContext(options))
        {
            db.Projects.Add(record);

            await db.SaveChangesAsync();
        }

        ProjectRecord? loadedRecord;

        await using (var db = new ProjectDbContext(options))
        {
            loadedRecord = await db.Projects
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    x => x.Id == original.Key.Id);
        }

        Assert.NotNull(loadedRecord);

        var loaded = new Project(
            new ProjectKey(
                loadedRecord.Id,
                loadedRecord.Name))
        {
            Description = loadedRecord.Description
        };

        Assert.Equal(original.Key.Id, loaded.Key.Id);
        Assert.Equal(original.Key.Name, loaded.Key.Name);
        Assert.Equal(original.Description, loaded.Description);

        Assert.True(original.Key.Equals(loaded.Key));
    }
}
