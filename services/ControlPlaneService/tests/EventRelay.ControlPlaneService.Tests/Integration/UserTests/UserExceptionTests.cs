using EventRelay.ControlPlaneService.Application.Exceptions;
using EventRelay.ControlPlaneService.Infrastructure.Persistence;
using EventRelay.ControlPlaneService.Infrastructure.Persistence.Exceptions;
using EventRelay.ControlPlaneService.Infrastructure.Persistence.Exceptions.Abstraction;
using EventRelay.ControlPlaneService.Infrastructure.Persistence.Exceptions.Recovery;
using EventRelay.ControlPlaneService.Infrastructure.Persistence.Models;
using EventRelay.ControlPlaneService.Tests.Helpers;
using Microsoft.EntityFrameworkCore;

namespace EventRelay.ControlPlaneService.Tests.Integration.UserTests;

public sealed class UserExceptionTests(CreatePostgresContainer fixture) : IClassFixture<CreatePostgresContainer>
{
    [Fact]
    public async Task DuplicateIdentity_ThrowsExpectedException()
    {
        var options = new DbContextOptionsBuilder<ControlPlaneDbContext>()
            .UseNpgsql(fixture.ConnectionString)
            .Options;
        var issuer = "test-issuer";
        var subject = Guid.NewGuid().ToString();
        await using (var db = new ControlPlaneDbContext(options))
        {
            db.Users.Add(new UserRecord
            {
                Id = Guid.CreateVersion7(),
                Name = "Test User",
                Issuer = issuer,
                Subject = subject
            });

            await db.SaveChangesAsync();
        }

        await using var secondDb = new ControlPlaneDbContext(options);
        secondDb.Users.Add(new UserRecord
        {
            Id = Guid.CreateVersion7(),
            Name = "Test User",
            Issuer = issuer,
            Subject = subject
        });

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


        var unitOfWork = new EfUnitOfWork(secondDb, pipeline);

        await Assert.ThrowsAsync<DuplicateUserIdentityException>(
            () => unitOfWork.SaveChangesAsync(CancellationToken.None));

        Assert.DoesNotContain(
            secondDb.ChangeTracker.Entries<UserRecord>(),
            entry => entry.State == EntityState.Added);
    }
}
