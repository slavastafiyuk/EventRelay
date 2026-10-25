using EventRelay.ControlPlaneService.Domain.Entities.Project;
using EventRelay.ControlPlaneService.Domain.Entities.User;
using EventRelay.ControlPlaneService.Infrastructure.Persistence;
using EventRelay.ControlPlaneService.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace EventRelay.ControlPlaneService.Tests.Integration.ProjectTests;

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
            new DbContextOptionsBuilder<ControlPlaneDbContext>()
                .UseNpgsql(postgres.GetConnectionString())
                .Options;


        await using (var db = new ControlPlaneDbContext(options))
        {
            await RelationalDatabaseFacadeExtensions.MigrateAsync(db.Database);

            var applied = await RelationalDatabaseFacadeExtensions.GetAppliedMigrationsAsync(db.Database);
            Assert.NotEmpty((System.Collections.IEnumerable)applied);

            var pending = await RelationalDatabaseFacadeExtensions.GetPendingMigrationsAsync(db.Database);
            Assert.Empty((System.Collections.IEnumerable)pending);
        }
        var owner = new UserKey("TestOwner");

        var original = new Project(
            new ProjectKey("MyStore"),
            owner)
        {
            Description = "Online shop"
        };

        var record = new ProjectRecord
        {
            Id = original.Key.Id,
            Name = original.Key.Name,
            Description = original.Description
        };

        await using (var db = new Infrastructure.Persistence.ControlPlaneDbContext((DbContextOptions<Infrastructure.Persistence.ControlPlaneDbContext>)options))
        {
            db.Projects.Add(record);

            await db.SaveChangesAsync();
        }

        ProjectRecord? loadedRecord;

        await using (var db = new Infrastructure.Persistence.ControlPlaneDbContext((DbContextOptions<Infrastructure.Persistence.ControlPlaneDbContext>)options))
        {
            loadedRecord = await db.Projects
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    x => x.Id == original.Key.Id);
        }

        Assert.NotNull(loadedRecord);

        var loaded = new Project(
            new ProjectKey(loadedRecord.Id, loadedRecord.Name),
            new UserKey(loadedRecord.OwnerId, loadedRecord.OwnerName))
        {
            Description = loadedRecord.Description
        };

        Assert.Equal(original.Key.Id, loaded.Key.Id);
        Assert.Equal(original.Key.Name, loaded.Key.Name);
        Assert.Equal(original.Description, loaded.Description);

        Assert.True(original.Key.Equals(loaded.Key));
    }
}
