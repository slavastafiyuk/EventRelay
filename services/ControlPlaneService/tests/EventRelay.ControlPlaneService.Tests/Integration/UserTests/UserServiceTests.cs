using EventRelay.ControlPlaneService.Application.Services;
using EventRelay.ControlPlaneService.Domain.Entities.User;
using EventRelay.ControlPlaneService.Infrastructure.Persistence;
using EventRelay.ControlPlaneService.Infrastructure.Persistence.Exceptions;
using EventRelay.ControlPlaneService.Infrastructure.Persistence.Exceptions.Abstraction;
using EventRelay.ControlPlaneService.Infrastructure.Persistence.Exceptions.Recovery;
using EventRelay.ControlPlaneService.Infrastructure.Persistence.Models.User;
using EventRelay.ControlPlaneService.Repository.Repositories;
using EventRelay.ControlPlaneService.Tests.Helpers;
using Microsoft.EntityFrameworkCore;

namespace EventRelay.ControlPlaneService.Tests.Integration.UserTests;

public sealed class UserServiceTests(CreatePostgresContainer fixture) : IClassFixture<CreatePostgresContainer>
{
    [Fact]
    public async Task GetOrCreate_Twice_PersistsOnlyOneUser()
    {
        var issuer = "http://localhost:8081/realms/eventrelay";
        var subject = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<ControlPlaneDbContext>()
            .UseNpgsql(fixture.ConnectionString)
            .Options;
        Guid firstId;

        var translator = new DbUpdateExceptionTranslator(
            new IDbUpdateExceptionHandler[]
            {
                new ProjectNameConflictHandler(),
                new UserIdentityConflictHandler()
            });

        var pipeline = new DbUpdateExceptionPipeline(translator,
            new IDbUpdateRecoveryStrategy[]
            {
                new DetachSingleAddedEntryRecovery<UserRecord>("IX_users_Issuer_Subject")
            });

        await using (var db = new ControlPlaneDbContext(options))
        {
            var service = new UserService(new UserRepository(db), new EfUnitOfWork(db, pipeline));

            var user = new User(
                new UserKey(Guid.CreateVersion7(), "Test User"),
                issuer,
                subject);

            var result = await service.GetOrCreateAsync(user, CancellationToken.None);

            firstId = result.Id;
        }

        await using (var db = new ControlPlaneDbContext(options))
        {
            var service = new UserService(new UserRepository(db), new EfUnitOfWork(db, pipeline));

            var user = new User(
                new UserKey(Guid.CreateVersion7(), "Test User"),
                issuer,
                subject);

            var result = await service.GetOrCreateAsync(user, CancellationToken.None);
            Assert.Equal(firstId, result.Id);

            var count = await db.Users.CountAsync(x =>
                                x.Issuer == issuer &&
                                x.Subject == subject);
            Assert.Equal(1, count);
        }
    }


    [Fact]
    public void Recovery_WithMultipleEntries_DoesNotDetachAnything()
    {
        var options = new DbContextOptionsBuilder<ControlPlaneDbContext>()
            .UseNpgsql("Host=localhost;Database=unused;Username=dummy;Password=dummy")
            .Options;

        using var db = new ControlPlaneDbContext(options);

        var first = new UserRecord
        {
            Id = Guid.CreateVersion7(),
            Name = "First",
            Issuer = "test",
            Subject = "first"
        };

        var second = new UserRecord
        {
            Id = Guid.CreateVersion7(),
            Name = "Second",
            Issuer = "test",
            Subject = "second"
        };

        db.Users.AddRange(first, second);

        var entries = new[]
        {
            db.Entry(first),
            db.Entry(second)
        };

        var exception = new DbUpdateException("Simulated failure", entries);

        var recovery = new DetachSingleAddedEntryRecovery<UserRecord>("IX_users_Issuer_Subject");

        Assert.False(recovery.TryRecover(exception));
        Assert.All(entries, entry => Assert.Equal(EntityState.Added, entry.State));
    }
}
