using EventRelay.ControlPlaneService.Domain.Entities.Project;
using EventRelay.ControlPlaneService.Domain.Entities.Project.Access;
using EventRelay.ControlPlaneService.Domain.Entities.User;
using EventRelay.ControlPlaneService.Infrastructure.Persistence;
using EventRelay.ControlPlaneService.Infrastructure.Persistence.Models.Project.Access;
using EventRelay.ControlPlaneService.Infrastructure.Persistence.Models.User;
using EventRelay.ControlPlaneService.Repository.Repositories;
using EventRelay.ControlPlaneService.Tests.Helpers;
using Microsoft.EntityFrameworkCore;

namespace EventRelay.ControlPlaneService.Tests.Integration.ProjectTests;

public sealed class ProjectPersistenceTests(CreatePostgresContainer fixture)
    : IClassFixture<CreatePostgresContainer>
{
    private ControlPlaneDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ControlPlaneDbContext>()
            .UseNpgsql(fixture.ConnectionString)
            .Options;

        return new ControlPlaneDbContext(options);
    }

    private static UserRecord CreateUserRecord(UserKey user) => new()
    {
        Id = user.Id,
        Name = user.Name,
        Issuer = "test",
        Subject = user.Id.ToString()
    };

    [Fact]
    public async Task Project_Relationships_CanBeLoadedIndividually()
    {
        // Arrange
        var owner = new UserKey(Guid.CreateVersion7(), "TestOwner");

        var project = new Project(
            new ProjectKey($"Debug-{Guid.NewGuid():N}"),
            owner);

        // 1. Save user and project.
        await using (var db = CreateDbContext())
        {
            db.Users.Add(CreateUserRecord(owner));
            await db.SaveChangesAsync();

            var repository = new ProjectRepository(db);

            await repository.AddAsync(
                project,
                CancellationToken.None);

            await db.SaveChangesAsync();
        }

        // 2. Load using a fresh DbContext.
        await using (var db = CreateDbContext())
        {
            var projectId = project.Key.Id;

            // CHECK 1: Project exists.
            var projectExists = await db.Projects
                .AnyAsync(x => x.Id == projectId);

            Assert.True(
                projectExists,
                "CHECK 1 FAILED: Project was not persisted.");

            // CHECK 2: Full UserRecord exists and has Name.
            var fullUser = await db.Users
                .AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == owner.Id);

            Assert.NotNull(fullUser);

            Assert.Equal(
                owner.Name,
                fullUser.Name);

            // CHECK 3: UserKeyRecord exists and has Name.
            var userKeyQuery = db.Set<UserKeyRecord>()
                .AsNoTracking()
                .Where(x => x.Id == owner.Id);

            var userKeySql = userKeyQuery.ToQueryString();

            var userKey = await userKeyQuery
                .SingleOrDefaultAsync();

            Assert.True(
                userKey is not null,
                $"CHECK 3 FAILED: UserKeyRecord not found.\n" +
                $"SQL:\n{userKeySql}");

            Assert.True(
                userKey.Name == owner.Name,
                $"CHECK 3 FAILED: UserKeyRecord.Name is incorrect.\n" +
                $"Expected: '{owner.Name}'\n" +
                $"Actual: '{userKey.Name}'\n" +
                $"SQL:\n{userKeySql}");

            // CHECK 4: ProjectAccessRecord exists.
            var accessExists = await db.Set<ProjectAccessRecord>()
                .AnyAsync(x => x.ProjectId == projectId);

            Assert.True(
                accessExists,
                "CHECK 4 FAILED: ProjectAccessRecord not persisted.");

            // CHECK 5: Project can be loaded without Includes.
            var projectRecord = await db.Projects
                .AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == projectId);

            Assert.NotNull(projectRecord);

            Assert.Equal(
                owner.Id,
                projectRecord.OwnerId);

            // CHECK 6: Owner navigation loads Name correctly.
            var ownerQuery = db.Projects
                .AsNoTracking()
                .Include(x => x.Owner)
                .Where(x => x.Id == projectId);

            var ownerSql = ownerQuery.ToQueryString();

            var withOwner = await ownerQuery
                .SingleOrDefaultAsync();

            Assert.True(
                withOwner is not null,
                $"CHECK 6 FAILED: Project with Owner not found.\n" +
                $"SQL:\n{ownerSql}");

            Assert.NotNull(withOwner.Owner);

            Assert.True(
                withOwner.Owner.Name == owner.Name,
                $"CHECK 6 FAILED: Owner navigation has wrong Name.\n" +
                $"Expected: '{owner.Name}'\n" +
                $"Actual: '{withOwner.Owner.Name}'\n" +
                $"SQL:\n{ownerSql}");

            // CHECK 7: Access navigation loads correctly.
            var withAccess = await db.Projects
                .AsNoTracking()
                .Include(x => x.Access)
                .SingleOrDefaultAsync(x => x.Id == projectId);

            Assert.NotNull(withAccess);
            Assert.NotNull(withAccess.Access);

            Assert.Equal(
                ProjectVisibility.Private,
                withAccess.Access.Visibility);

            // CHECK 8: Full repository query loads the project.
            var repository = new ProjectRepository(db);

            var loaded = await repository.GetByIdAsync(
                projectId,
                CancellationToken.None);

            Assert.NotNull(loaded);

            // CHECK 9: Mapster preserves the owner's Name.
            Assert.Equal(owner.Id, loaded.Owner.Id);

            Assert.True(
                loaded.Owner.Name == owner.Name,
                $"CHECK 9 FAILED: Domain Project.Owner.Name is wrong.\n" +
                $"Expected: '{owner.Name}'\n" +
                $"Actual: '{loaded.Owner.Name}'");

            // CHECK 10: Domain Project is reconstructed correctly.
            Assert.Equal(project.Key.Id, loaded.Key.Id);
            Assert.Equal(project.Key.Name, loaded.Key.Name);

            Assert.Equal(
                ProjectVisibility.Private,
                loaded.Access.Visibility);

            Assert.Empty(loaded.Access.Members);
        }
    }
}
