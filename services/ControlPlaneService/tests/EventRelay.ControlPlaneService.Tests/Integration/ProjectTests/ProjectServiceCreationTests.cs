
using EventRelay.ControlPlaneService.Application.Abstractions.Authentication;
using EventRelay.ControlPlaneService.Application.Services;
using EventRelay.ControlPlaneService.Domain.Entities.Organization;
using EventRelay.ControlPlaneService.Domain.Entities.Organization.Access;
using EventRelay.ControlPlaneService.Domain.Entities.Project;
using EventRelay.ControlPlaneService.Domain.Entities.Project.Access;
using EventRelay.ControlPlaneService.Domain.Entities.User;
using EventRelay.ControlPlaneService.Infrastructure.Persistence;
using EventRelay.ControlPlaneService.Repository.Repositories;
using EventRelay.ControlPlaneService.Tests.Helpers;
using Microsoft.EntityFrameworkCore;

namespace EventRelay.ControlPlaneService.Tests.Integration.ProjectTests;

public sealed class ProjectServiceCreationTests(
    CreatePostgresContainer fixture)
    : IClassFixture<CreatePostgresContainer>
{
    private ControlPlaneDbContext CreateDbContext()
    {
        var options =
            new DbContextOptionsBuilder<ControlPlaneDbContext>()
                .UseNpgsql(fixture.ConnectionString)
                .Options;

        return new ControlPlaneDbContext(options);
    }

    private static EfUnitOfWork CreateUnitOfWork(
        ControlPlaneDbContext db)
    {
        return new EfUnitOfWork(
            db,
            new DbUpdateExceptionPipeline([], []));
    }

    private static OrganizationService CreateOrganizationService(
        ControlPlaneDbContext db,
        UserKey actor)
    {
        var currentUser = new CurrentUser();
        currentUser.Initialize(actor);

        var unitOfWork = CreateUnitOfWork(db);

        var userService = new UserService(
            new UserRepository(db),
            unitOfWork);

        return new OrganizationService(
            new OrganizationRepository(db),
            unitOfWork,
            userService,
            currentUser);
    }

    private static ProjectService CreateProjectService(
        ControlPlaneDbContext db,
        UserKey actor)
    {
        var currentUser = new CurrentUser();
        currentUser.Initialize(actor);

        var unitOfWork = CreateUnitOfWork(db);

        var userService = new UserService(
            new UserRepository(db),
            unitOfWork);

        var organizationService = new OrganizationService(
            new OrganizationRepository(db),
            unitOfWork,
            userService,
            currentUser);

        return new ProjectService(
            new ProjectRepository(db),
            organizationService,
            unitOfWork,
            userService,
            currentUser);
    }

    private async Task<UserKey> ProvisionUserAsync()
    {
        var subject = $"user-{Guid.NewGuid():N}";

        await OrganizationTestHelper.CreateAsync(
            fixture.Client, subject);

        await using var db = CreateDbContext();

        var user = await db.Users
            .AsNoTracking()
            .SingleAsync(x => x.Subject == subject);

        return new UserKey(user.Id, user.Name);
    }

    private async Task<OrganizationKey> CreateOrganizationAsync(
        UserKey owner)
    {
        await using var db = CreateDbContext();

        var service = CreateOrganizationService(db, owner);

        return await service.CreateAsync(
            new Organization(
                new OrganizationKey($"Org-{Guid.NewGuid():N}"),
                owner),
            CancellationToken.None);
    }


    [Fact]
    public async Task Create_WithDifferentOwner_ThrowsUnauthorized()
    {
        var actor = await ProvisionUserAsync();
        var otherUser = await ProvisionUserAsync();

        var organization = await CreateOrganizationAsync(actor);

        await using var db = CreateDbContext();

        // Both users belong to the same organization.
        var organizationService = CreateOrganizationService(
            db, actor);

        await organizationService.AddMemberAsync(
            organization.Id,
            otherUser.Id,
            OrganizationRole.Member,
            CancellationToken.None);

        var projectService = CreateProjectService(db, actor);

        var project = new Project(
            new ProjectKey($"Spoofed-{Guid.NewGuid():N}"),
            otherUser,
            organization);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => projectService.CreateAsync(
                project, CancellationToken.None));

        Assert.False(await db.Projects.AnyAsync(
            x => x.Id == project.Key.Id));
    }



    [Fact]
    public async Task Create_WithIncorrectOrganizationName_RefreshesKey()
    {
        var actor = await ProvisionUserAsync();

        var organization = await CreateOrganizationAsync(actor);

        await using var db = CreateDbContext();

        var service = CreateProjectService(db, actor);

        var project = new Project(
            new ProjectKey($"Canonical-{Guid.NewGuid():N}"),
            actor,
            new OrganizationKey(
                organization.Id,
                "Incorrect organization name"));

        var result = await service.CreateAsync(
            project, CancellationToken.None);

        Assert.Equal(project.Key.Id, result.Id);

        Assert.NotNull(project.Organization);

        Assert.Equal(
            organization.Id,
            project.Organization.Id);

        Assert.Equal(
            organization.Name,
            project.Organization.Name);

        Assert.True(await db.Projects.AnyAsync(
            x => x.Id == project.Key.Id));
    }


    [Fact]
    public async Task Create_WithoutOrganization_Throws()
    {
        var actor = await ProvisionUserAsync();

        await using var db = CreateDbContext();

        var service = CreateProjectService(db, actor);

        var project = new Project(
            new ProjectKey($"Invalid-{Guid.NewGuid():N}"),
            actor);

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => service.CreateAsync(
                project, CancellationToken.None));

        Assert.False(await db.Projects.AnyAsync(
            x => x.Id == project.Key.Id));
    }

    [Fact]
    public async Task Create_AsNonMember_ThrowsKeyNotFound()
    {
        var owner = await ProvisionUserAsync();
        var outsider = await ProvisionUserAsync();

        var organization = await CreateOrganizationAsync(owner);

        await using var db = CreateDbContext();

        var service = CreateProjectService(db, outsider);

        var project = new Project(
            new ProjectKey($"Forbidden-{Guid.NewGuid():N}"),
            outsider,
            organization);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => service.CreateAsync(
                project, CancellationToken.None));

        Assert.False(await db.Projects.AnyAsync(
            x => x.Id == project.Key.Id));
    }

    [Fact]
    public async Task Create_WithPreassignedMembers_Throws()
    {
        var actor = await ProvisionUserAsync();
        var member = await ProvisionUserAsync();

        var organization = await CreateOrganizationAsync(actor);

        await using var db = CreateDbContext();

        var organizationService = CreateOrganizationService(
            db, actor);

        await organizationService.AddMemberAsync(
            organization.Id,
            member.Id,
            OrganizationRole.Member,
            CancellationToken.None);

        var service = CreateProjectService(db, actor);

        var project = new Project(
            new ProjectKey($"Members-{Guid.NewGuid():N}"),
            actor,
            organization);

        project.ChangeVisibility(ProjectVisibility.Internal);
        project.AddMember(member, ProjectRole.Editor);

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.CreateAsync(
                project, CancellationToken.None));

        Assert.False(await db.Projects.AnyAsync(
            x => x.Id == project.Key.Id));
    }

    [Fact]
    public async Task Create_WithNonexistentOrganization_ThrowsKeyNotFound()
    {
        var actor = await ProvisionUserAsync();

        await using var db = CreateDbContext();

        var service = CreateProjectService(db, actor);

        var project = new Project(
            new ProjectKey($"Invalid-{Guid.NewGuid():N}"),
            actor,
            new OrganizationKey(
                Guid.CreateVersion7(),
                "Nonexistent organization"));

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => service.CreateAsync(
                project,
                CancellationToken.None));

        Assert.False(await db.Projects.AnyAsync(
            x => x.Id == project.Key.Id));
    }

}
