
using EventRelay.ControlPlaneService.Domain.Entities.Organization;
using EventRelay.ControlPlaneService.Domain.Entities.Organization.Access;
using EventRelay.ControlPlaneService.Domain.Entities.User;
using EventRelay.ControlPlaneService.Infrastructure.Persistence;
using EventRelay.ControlPlaneService.Infrastructure.Persistence.Models.User;
using EventRelay.ControlPlaneService.Repository.Repositories;
using EventRelay.ControlPlaneService.Tests.Helpers;
using Microsoft.EntityFrameworkCore;

namespace EventRelay.ControlPlaneService.Tests.Integration.OrganizationTests;

public sealed class OrganizationPersistenceTests(
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

    [Fact]
    public async Task Organization_CanBePersistedAndRestored()
    {
        var owner = new UserKey(Guid.CreateVersion7(), "Owner");
        var user = new UserKey(Guid.CreateVersion7(), "Member");

        var organization = new Organization(
            new OrganizationKey("Acme"),
            owner);

        organization.AddMember(user, OrganizationRole.Member);

        await using (var db = CreateDbContext())
        {
            db.Users.AddRange(
                CreateUser(owner),
                CreateUser(user));

            await db.SaveChangesAsync();

            var repository = new OrganizationRepository(db);

            await repository.AddAsync(
                organization,
                CancellationToken.None);

            await db.SaveChangesAsync();
        }

        await using (var db = CreateDbContext())
        {
            var repository = new OrganizationRepository(db);

            var restored = await repository.GetByIdAsync(
                organization.Key.Id,
                CancellationToken.None);

            Assert.NotNull(restored);

            Assert.Equal(organization.Key.Id, restored.Key.Id);
            Assert.Equal(organization.Key.Name, restored.Key.Name);
            Assert.Equal(owner.Id, restored.Owner.Id);

            Assert.Equal(2, restored.Members.Count);

            Assert.True(restored.IsOwner(owner));
            Assert.True(restored.IsAdmin(owner));
            Assert.True(restored.IsMember(user));

            var member = Assert.Single(
                restored.Members,
                m => m.User.Id == user.Id);

            Assert.Equal(OrganizationRole.Member, member.Role);

            Assert.True(
                (restored.CreatedAt - organization.CreatedAt)
                .Duration() < TimeSpan.FromMilliseconds(1));
        }
    }

    private static UserRecord CreateUser(UserKey user) => new()
    {
        Id = user.Id,
        Name = user.Name,
        Issuer = "test",
        Subject = user.Id.ToString()
    };
}
