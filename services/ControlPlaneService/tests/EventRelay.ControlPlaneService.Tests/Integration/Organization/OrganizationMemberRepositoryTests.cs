
using EventRelay.ControlPlaneService.Api.Configuration;
using EventRelay.ControlPlaneService.Application.Exceptions.Organization;
using EventRelay.ControlPlaneService.Domain.Entities.Organization;
using EventRelay.ControlPlaneService.Domain.Entities.Organization.Access;
using EventRelay.ControlPlaneService.Domain.Entities.User;
using EventRelay.ControlPlaneService.Infrastructure.Persistence;
using EventRelay.ControlPlaneService.Infrastructure.Persistence.Exceptions;
using EventRelay.ControlPlaneService.Infrastructure.Persistence.Exceptions.Abstraction;
using EventRelay.ControlPlaneService.Infrastructure.Persistence.Models.Organization;
using EventRelay.ControlPlaneService.Infrastructure.Persistence.Models.User;
using EventRelay.ControlPlaneService.Repository.Repositories;
using EventRelay.ControlPlaneService.Tests.Helpers;
using Microsoft.EntityFrameworkCore;

namespace EventRelay.ControlPlaneService.Tests.Integration.OrganizationTests;

public sealed class OrganizationMemberRepositoryTests(
    CreatePostgresContainer fixture)
    : IClassFixture<CreatePostgresContainer>
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    private ControlPlaneDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ControlPlaneDbContext>()
            .UseNpgsql(fixture.ConnectionString)
            .Options;

        return new ControlPlaneDbContext(options);
    }

    private static UserKey NewUser(string name) =>
        new(Guid.CreateVersion7(), name);

    private static UserRecord CreateUserRecord(UserKey user) => new()
    {
        Id = user.Id,
        Name = user.Name,
        Issuer = "integration-test",
        Subject = user.Id.ToString()
    };

    private async Task<(
        Guid OrganizationId,
        UserKey Owner,
        UserKey Member,
        UserKey OtherMember)> SeedOrganizationAsync(
        bool includeMember = false,
        bool includeOtherMember = false)
    {
        MapsterInitialization.Initialize();

        var owner = NewUser("Owner");
        var member = NewUser("Member");
        var otherMember = NewUser("Other Member");

        var organization = new Organization(
            new OrganizationKey($"Organization-{Guid.NewGuid():N}"),
            owner);

        if (includeMember)
            organization.AddMember(member, OrganizationRole.Member);

        if (includeOtherMember)
            organization.AddMember(otherMember, OrganizationRole.Member);

        await using (var db = CreateDbContext())
        {
            db.Users.AddRange(
                CreateUserRecord(owner),
                CreateUserRecord(member),
                CreateUserRecord(otherMember));

            await db.SaveChangesAsync();

            var repository = new OrganizationRepository(db);

            await repository.AddAsync(organization, Ct);
            await db.SaveChangesAsync();
        }

        return (organization.Key.Id, owner, member, otherMember);
    }

    [Fact]
    public async Task AddMemberAsync_PersistsMemberWithRoleAndJoinedAt()
    {
        // Arrange
        var seed = await SeedOrganizationAsync();

        var joinedAt = new DateTimeOffset(
            2026, 11, 12, 10, 0, 0, TimeSpan.Zero);

        var member = OrganizationMember.Restore(
            seed.Member,
            OrganizationRole.Member,
            joinedAt);

        // Act
        await using (var db = CreateDbContext())
        {
            var repository = new OrganizationRepository(db);

            await repository.AddMemberAsync(
                seed.OrganizationId,
                member,
                Ct);

            await db.SaveChangesAsync();
        }

        // Assert using a fresh DbContext.
        await using (var db = CreateDbContext())
        {
            var record = await db.Set<OrganizationMemberRecord>()
                .AsNoTracking()
                .SingleAsync(x =>
                    x.OrganizationId == seed.OrganizationId &&
                    x.UserId == seed.Member.Id);

            Assert.Equal(seed.OrganizationId, record.OrganizationId);
            Assert.Equal(seed.Member.Id, record.UserId);
            Assert.Equal(OrganizationRole.Member, record.Role);
            Assert.Equal(joinedAt, record.JoinedAt);

            // Verify database -> repository -> domain mapping too.
            var repository = new OrganizationRepository(db);
            var organization = await repository.GetByIdAsync(
                seed.OrganizationId, Ct);

            Assert.NotNull(organization);
            Assert.Equal(2, organization.Members.Count);
            Assert.True(organization.IsMember(seed.Member));
        }
    }

    [Fact]
    public async Task ChangeMemberRoleAsync_UpdatesOnlyRole()
    {
        // Arrange
        var seed = await SeedOrganizationAsync(includeMember: true);

        DateTimeOffset originalJoinedAt;

        await using (var db = CreateDbContext())
        {
            var record = await db.Set<OrganizationMemberRecord>()
                .AsNoTracking()
                .SingleAsync(x =>
                    x.OrganizationId == seed.OrganizationId &&
                    x.UserId == seed.Member.Id);

            originalJoinedAt = record.JoinedAt;
            Assert.Equal(OrganizationRole.Member, record.Role);
        }

        // Act: the repository attaches a detached entity
        // and marks only Role as modified.
        await using (var db = CreateDbContext())
        {
            var repository = new OrganizationRepository(db);

            await repository.ChangeMemberRoleAsync(
                seed.OrganizationId,
                seed.Member.Id,
                OrganizationRole.Admin,
                Ct);

            await db.SaveChangesAsync();
        }

        // Assert
        await using (var db = CreateDbContext())
        {
            var member = await db.Set<OrganizationMemberRecord>()
                .AsNoTracking()
                .SingleAsync(x =>
                    x.OrganizationId == seed.OrganizationId &&
                    x.UserId == seed.Member.Id);

            Assert.Equal(OrganizationRole.Admin, member.Role);

            // Critical: the default JoinedAt value in the
            // detached stub must NOT overwrite the stored value.
            Assert.Equal(originalJoinedAt, member.JoinedAt);

            var organization = await new OrganizationRepository(db)
                .GetByIdAsync(seed.OrganizationId, Ct);

            Assert.NotNull(organization);
            Assert.True(organization.IsAdmin(seed.Member));
            Assert.True(organization.IsAdmin(seed.Owner));
            Assert.Equal(2, organization.Members.Count);
        }
    }

    [Fact]
    public async Task RemoveMemberAsync_RemovesOnlyRequestedMember()
    {
        // Arrange
        var seed = await SeedOrganizationAsync(
            includeMember: true,
            includeOtherMember: true);

        // Act
        await using (var db = CreateDbContext())
        {
            var repository = new OrganizationRepository(db);

            await repository.RemoveMemberAsync(
                seed.OrganizationId,
                seed.Member.Id,
                Ct);

            await db.SaveChangesAsync();
        }

        // Assert
        await using (var db = CreateDbContext())
        {
            var members = await db.Set<OrganizationMemberRecord>()
                .AsNoTracking()
                .Where(x => x.OrganizationId == seed.OrganizationId)
                .ToListAsync();

            Assert.Equal(2, members.Count);

            Assert.DoesNotContain(
                members,
                x => x.UserId == seed.Member.Id);

            Assert.Contains(
                members,
                x => x.UserId == seed.Owner.Id);

            Assert.Contains(
                members,
                x => x.UserId == seed.OtherMember.Id);

            var organization = await new OrganizationRepository(db)
                .GetByIdAsync(seed.OrganizationId, Ct);

            Assert.NotNull(organization);
            Assert.False(organization.IsMember(seed.Member));
            Assert.True(organization.IsMember(seed.OtherMember));
        }
    }

    [Fact]
    public async Task MemberOperations_AreScopedToOrganizationId()
    {
        // Arrange: same user belongs to two organizations.
        var seed = await SeedOrganizationAsync(includeMember: true);

        var secondOrganization = new Organization(
            new OrganizationKey($"Other-{Guid.NewGuid():N}"),
            seed.Owner);

        secondOrganization.AddMember(
            seed.Member,
            OrganizationRole.Member);

        await using (var db = CreateDbContext())
        {
            var repository = new OrganizationRepository(db);

            await repository.AddAsync(secondOrganization, Ct);
            await db.SaveChangesAsync();
        }

        // Change role in the first organization.
        await using (var db = CreateDbContext())
        {
            var repository = new OrganizationRepository(db);

            await repository.ChangeMemberRoleAsync(
                seed.OrganizationId,
                seed.Member.Id,
                OrganizationRole.Admin,
                Ct);

            await db.SaveChangesAsync();
        }

        // Verify the second organization's role was not changed.
        await using (var db = CreateDbContext())
        {
            var memberships = await db.Set<OrganizationMemberRecord>()
                .AsNoTracking()
                .Where(x => x.UserId == seed.Member.Id)
                .ToListAsync();

            Assert.Equal(2, memberships.Count);

            Assert.Equal(
                OrganizationRole.Admin,
                Assert.Single(
                    memberships,
                    x => x.OrganizationId == seed.OrganizationId).Role);

            Assert.Equal(
                OrganizationRole.Member,
                Assert.Single(
                    memberships,
                    x => x.OrganizationId == secondOrganization.Key.Id).Role);
        }

        // Remove membership from only the first organization.
        await using (var db = CreateDbContext())
        {
            var repository = new OrganizationRepository(db);

            await repository.RemoveMemberAsync(
                seed.OrganizationId,
                seed.Member.Id,
                Ct);

            await db.SaveChangesAsync();
        }

        // The second organization's membership must still exist.
        await using (var db = CreateDbContext())
        {
            var remaining = await db.Set<OrganizationMemberRecord>()
                .AsNoTracking()
                .Where(x => x.UserId == seed.Member.Id)
                .ToListAsync();

            var membership = Assert.Single(remaining);

            Assert.Equal(
                secondOrganization.Key.Id,
                membership.OrganizationId);

            Assert.Equal(OrganizationRole.Member, membership.Role);
        }
    }

    [Fact]
    public async Task AddMemberAsync_DuplicateMembership_ThrowsDatabaseException()
    {
        // Arrange: membership already exists.
        var seed = await SeedOrganizationAsync(includeMember: true);

        var duplicate = OrganizationMember.Restore(
            seed.Member,
            OrganizationRole.Admin,
            DateTimeOffset.UtcNow);

        // Act + Assert: PostgreSQL must enforce the composite PK.
        await using (var db = CreateDbContext())
        {
            var repository = new OrganizationRepository(db);

            await repository.AddMemberAsync(
                seed.OrganizationId,
                duplicate,
                Ct);

            await Assert.ThrowsAsync<DbUpdateException>(
                () => db.SaveChangesAsync());
        }

        // The rejected insert must not modify the existing member.
        await using (var db = CreateDbContext())
        {
            var memberships = await db.Set<OrganizationMemberRecord>()
                .AsNoTracking()
                .Where(x =>
                    x.OrganizationId == seed.OrganizationId &&
                    x.UserId == seed.Member.Id)
                .ToListAsync();

            var existing = Assert.Single(memberships);

            Assert.Equal(OrganizationRole.Member, existing.Role);
        }
    }


    [Fact]
    public async Task AddMemberAsync_DuplicateMembership_TranslatesToConflict()
    {
        var seed = await SeedOrganizationAsync(includeMember: true);

        var duplicate = OrganizationMember.Restore(
            seed.Member,
            OrganizationRole.Member,
            DateTimeOffset.UtcNow);

        await using var db = CreateDbContext();

        var repository = new OrganizationRepository(db);
        var unitOfWork = new EfUnitOfWork(db, CreatePipeline());

        await repository.AddMemberAsync(
            seed.OrganizationId,
            duplicate,
            Ct);

        var exception =
            await Assert.ThrowsAsync<OrganizationMembershipConflictException>(
                async () => await unitOfWork.SaveChangesAsync(Ct));

        Assert.IsType<DbUpdateException>(exception.InnerException);
    }


    [Fact]
    public async Task ChangeMemberRoleAsync_DeletedBeforeSave_TranslatesToConflict()
    {
        var seed = await SeedOrganizationAsync(includeMember: true);

        // Request A prepares an update but hasn't saved yet.
        await using var firstDb = CreateDbContext();

        var firstRepository = new OrganizationRepository(firstDb);

        await firstRepository.ChangeMemberRoleAsync(
            seed.OrganizationId,
            seed.Member.Id,
            OrganizationRole.Admin,
            Ct);

        await using (var secondDb = CreateDbContext())
        {
            var secondRepository = new OrganizationRepository(secondDb);

            await secondRepository.RemoveMemberAsync(
                seed.OrganizationId,
                seed.Member.Id,
                Ct);

            await secondDb.SaveChangesAsync();
        }

        var unitOfWork = new EfUnitOfWork(
            firstDb,
            CreatePipeline());

        var exception =
            await Assert.ThrowsAsync<OrganizationMembershipConflictException>(
                async () => await unitOfWork.SaveChangesAsync(Ct));

        Assert.IsType<DbUpdateConcurrencyException>(
            exception.InnerException);
    }


    #region Helper Methods


    private static DbUpdateExceptionPipeline CreatePipeline()
    {
        return new DbUpdateExceptionPipeline(
            new IDbUpdateExceptionTranslator[]
            {
            new PostgresUniqueViolationTranslator(
                new IDbUpdateExceptionHandler[]
                {
                    new OrganizationMemberConflictHandler()
                }),

            new EfConcurrencyExceptionTranslator(
                new IDbUpdateConcurrencyHandler[]
                {
                    new OrganizationMemberConcurrencyHandler()
                })
            },
            Array.Empty<IDbUpdateRecoveryStrategy>());
    }


    #endregion Helper Methods
}
