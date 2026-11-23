
using EventRelay.ControlPlaneService.Domain.Entities.Organization;
using EventRelay.ControlPlaneService.Domain.Entities.Project;
using EventRelay.ControlPlaneService.Domain.Entities.User;
using EventRelay.ControlPlaneService.Infrastructure.Persistence;
using EventRelay.ControlPlaneService.Infrastructure.Persistence.Models.User;
using EventRelay.ControlPlaneService.Repository.Repositories;
using EventRelay.ControlPlaneService.Tests.Helpers;
using Microsoft.EntityFrameworkCore;

namespace EventRelay.ControlPlaneService.Tests.Integration.ProjectTests;

public sealed class ProjectOrganizationPersistenceTests(
    CreatePostgresContainer fixture)
    : IClassFixture<CreatePostgresContainer>
{
    private ControlPlaneDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ControlPlaneDbContext>()
            .UseNpgsql(fixture.ConnectionString)
            .Options;

        return new ControlPlaneDbContext(options);
    }

    private static UserRecord CreateUser(UserKey user) => new()
    {
        Id = user.Id,
        Name = user.Name,
        Issuer = "test",
        Subject = user.Id.ToString()
    };

    [Fact]
    public async Task Project_WithOrganization_PersistsAndRestoresRelationship()
    {
        var owner = new UserKey(
            Guid.CreateVersion7(),
            "Organization owner");

        var organization = new Organization(
            new OrganizationKey($"Org-{Guid.NewGuid():N}"),
            owner);

        var project = new Project(
            new ProjectKey($"Project-{Guid.NewGuid():N}"),
            owner,
            organization.Key);

        await using (var db = CreateDbContext())
        {
            db.Users.Add(CreateUser(owner));
            await db.SaveChangesAsync();

            var organizationRepository = new OrganizationRepository(db);

            await organizationRepository.AddAsync(
                organization,
                CancellationToken.None);

            await db.SaveChangesAsync();

            var projectRepository = new ProjectRepository(db);

            await projectRepository.AddAsync(
                project,
                CancellationToken.None);

            await db.SaveChangesAsync();
        }

        await using (var db = CreateDbContext())
        {
            var projectRecord = await db.Projects
                .AsNoTracking()
                .SingleAsync(x => x.Id == project.Key.Id);

            Assert.Equal(
                organization.Key.Id,
                projectRecord.OrganizationId);

            var projectRepository = new ProjectRepository(db);

            var restored = await projectRepository.GetByIdAsync(
                project.Key.Id,
                CancellationToken.None);

            Assert.NotNull(restored);
            Assert.NotNull(restored.Organization);

            Assert.Equal(
                organization.Key.Id,
                restored.Organization.Id);

            Assert.Equal(
                organization.Key.Name,
                restored.Organization.Name);

            Assert.Equal(
                owner.Id,
                restored.Owner.Id);
        }
    }

    [Fact]
    public async Task Project_WithNonexistentOrganization_IsRejectedByDatabase()
    {
        var owner = new UserKey(
            Guid.CreateVersion7(),
            "Test owner");

        var nonexistentOrganization = new OrganizationKey(
            Guid.CreateVersion7(),
            "Nonexistent organization");

        var project = new Project(
            new ProjectKey($"Invalid-{Guid.NewGuid():N}"),
            owner,
            nonexistentOrganization);

        await using var db = CreateDbContext();

        db.Users.Add(CreateUser(owner));
        await db.SaveChangesAsync();

        var repository = new ProjectRepository(db);

        await repository.AddAsync(
            project,
            CancellationToken.None);

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }
}
