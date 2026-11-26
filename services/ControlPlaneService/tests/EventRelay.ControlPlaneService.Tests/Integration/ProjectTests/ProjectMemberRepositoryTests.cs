using EventRelay.ControlPlaneService.Api.Configuration;
using EventRelay.ControlPlaneService.Application.Exceptions.Project;
using EventRelay.ControlPlaneService.Domain.Entities.Organization;
using EventRelay.ControlPlaneService.Domain.Entities.Organization.Access;
using EventRelay.ControlPlaneService.Domain.Entities.Project;
using EventRelay.ControlPlaneService.Domain.Entities.Project.Access;
using EventRelay.ControlPlaneService.Domain.Entities.User;
using EventRelay.ControlPlaneService.Infrastructure.Persistence;
using EventRelay.ControlPlaneService.Infrastructure.Persistence.Exceptions;
using EventRelay.ControlPlaneService.Infrastructure.Persistence.Models.Project.Access;
using EventRelay.ControlPlaneService.Infrastructure.Persistence.Models.User;
using EventRelay.ControlPlaneService.Repository.Repositories;
using EventRelay.ControlPlaneService.Tests.Helpers;
using Microsoft.EntityFrameworkCore;

namespace EventRelay.ControlPlaneService.Tests.Integration.ProjectTests;

public sealed class ProjectMemberRepositoryTests(CreatePostgresContainer fixture) : IClassFixture<CreatePostgresContainer>
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    private ControlPlaneDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ControlPlaneDbContext>()
                .UseNpgsql(fixture.ConnectionString)
                .Options;

        return new ControlPlaneDbContext(options);
    }

    private static DbUpdateExceptionPipeline CreatePipeline()
    {
        return new DbUpdateExceptionPipeline(
            [
                new PostgresUniqueViolationTranslator(
                    [new ProjectMemberConflictHandler()]),

                new EfConcurrencyExceptionTranslator(
                    [new ProjectMemberConcurrencyHandler()])
            ],
            []);
    }

    private static UserRecord CreateUserRecord(UserKey user) => new()
    {
        Id = user.Id,
        Name = user.Name,
        Issuer = "integration-test",
        Subject = user.Id.ToString()
    };

    private async Task<(ProjectKey Project, UserKey Member)> SeedProjectAsync()
    {
        MapsterInitialization.Initialize();

        var owner = new UserKey(Guid.CreateVersion7(), "Owner");
        var member = new UserKey(Guid.CreateVersion7(), "Member");

        var organization = new Organization(
            new OrganizationKey($"Organization-{Guid.NewGuid():N}"),
            owner);

        organization.AddMember(member, OrganizationRole.Member);

        var project = new Project(
            new ProjectKey($"Project-{Guid.NewGuid():N}"),
            owner,
            organization.Key);

        project.ChangeVisibility(ProjectVisibility.Internal);
        project.AddMember(member, ProjectRole.Viewer);

        await using var db = CreateDbContext();

        db.Users.AddRange(
            CreateUserRecord(owner),
            CreateUserRecord(member));

        await db.SaveChangesAsync(Ct);
        await new OrganizationRepository(db).AddAsync(organization, Ct);
        await db.SaveChangesAsync(Ct);
        await new ProjectRepository(db).AddAsync(project, Ct);
        await db.SaveChangesAsync(Ct);
        return (project.Key, member);
    }

    [Fact]
    public async Task AddMemberAsync_DuplicateMembership_TranslatesToConflict()
    {
        var seed = await SeedProjectAsync();

        var duplicate = new ProjectMember(
            seed.Member, ProjectRole.Editor);

        await using (var db = CreateDbContext())
        {
            var repository = new ProjectRepository(db);
            var unitOfWork = new EfUnitOfWork(db, CreatePipeline());

            await repository.AddMemberAsync(
                seed.Project, duplicate, Ct);

            var exception =
                await Assert.ThrowsAsync<ProjectMembershipConflictException>(
                    () => unitOfWork.SaveChangesAsync(Ct));

            Assert.IsType<DbUpdateException>(exception.InnerException);
        }

        await using var verificationDb = CreateDbContext();

        var existing = await verificationDb
            .Set<ProjectMemberRecord>()
            .AsNoTracking()
            .SingleAsync(x =>
                x.ProjectId == seed.Project.Id &&
                x.UserId == seed.Member.Id, Ct);

        Assert.Equal(ProjectRole.Viewer, existing.Role);
    }

    [Fact]
    public async Task ChangeMemberRoleAsync_DeletedBeforeSave_TranslatesToConflict()
    {
        var seed = await SeedProjectAsync();
        await using var firstDb = CreateDbContext();
        var firstRepository = new ProjectRepository(firstDb);
        var updatedMember = new ProjectMember(seed.Member, ProjectRole.Editor);

        await firstRepository.ChangeMemberRoleAsync(seed.Project, updatedMember, Ct);

        await using (var secondDb = CreateDbContext())
        {
            var secondRepository = new ProjectRepository(secondDb);

            await secondRepository.RemoveMemberAsync(seed.Project, seed.Member, Ct);
            await secondDb.SaveChangesAsync(Ct);
        }

        var unitOfWork = new EfUnitOfWork(firstDb, CreatePipeline());

        var exception =
            await Assert.ThrowsAsync<ProjectMembershipConflictException>(
                () => unitOfWork.SaveChangesAsync(Ct));

        Assert.IsType<DbUpdateConcurrencyException>(
            exception.InnerException);

        await using var verificationDb = CreateDbContext();

        Assert.False(await verificationDb
            .Set<ProjectMemberRecord>()
            .AnyAsync(x =>
                x.ProjectId == seed.Project.Id &&
                x.UserId == seed.Member.Id, Ct));
    }
}
