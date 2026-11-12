
using EventRelay.ControlPlaneService.Domain.Entities.Organization;
using EventRelay.ControlPlaneService.Domain.Entities.Organization.Access;
using EventRelay.ControlPlaneService.Domain.Entities.User;
using EventRelay.ControlPlaneService.Domain.Exceptions.Organization;

namespace EventRelay.ControlPlaneService.Tests.Unit.OrganizationTests;

public sealed class OrganizationDomainTests
{
    private static UserKey NewUser() =>
        new(Guid.CreateVersion7(), $"user-{Guid.NewGuid():N}");

    private static Organization NewOrganization(UserKey owner) =>
        new(new OrganizationKey($"Org-{Guid.NewGuid():N}"), owner);

    [Fact]
    public void Create_RegistersOwnerAsAdmin()
    {
        var owner = NewUser();
        var organization = NewOrganization(owner);

        var member = Assert.Single(organization.Members);

        Assert.Equal(owner.Id, member.User.Id);
        Assert.Equal(OrganizationRole.Admin, member.Role);
        Assert.True(organization.IsOwner(owner));
        Assert.True(organization.IsAdmin(owner));
    }

    [Fact]
    public void AddMember_SameUserIdTwice_Throws()
    {
        var organization = NewOrganization(NewUser());
        var member = NewUser();

        organization.AddMember(member, OrganizationRole.Member);

        // Same identity, different UserKey instance.
        var duplicate = new UserKey(member.Id, "Different name");

        Assert.Throws<OrganizationRuleViolationException>(() =>
            organization.AddMember(duplicate, OrganizationRole.Admin));

        Assert.Equal(2, organization.Members.Count);
        Assert.False(organization.IsAdmin(member));
    }

    [Fact]
    public void ChangeRole_PreservesMembershipTimestamp()
    {
        var organization = NewOrganization(NewUser());
        var member = NewUser();

        organization.AddMember(member, OrganizationRole.Member);

        var before = Assert.Single(
            organization.Members, m => m.User.Id == member.Id);

        var joinedAt = before.JoinedAt;

        organization.ChangeMemberRole(member, OrganizationRole.Admin);

        var after = Assert.Single(
            organization.Members, m => m.User.Id == member.Id);

        Assert.Equal(OrganizationRole.Admin, after.Role);
        Assert.Equal(joinedAt, after.JoinedAt);
    }

    [Fact]
    public void Owner_CannotBeRemovedOrDemoted()
    {
        var owner = NewUser();
        var organization = NewOrganization(owner);

        Assert.Throws<OrganizationRuleViolationException>(() =>
            organization.RemoveMember(owner));

        Assert.Throws<OrganizationRuleViolationException>(() =>
            organization.ChangeMemberRole(owner, OrganizationRole.Member));

        Assert.True(organization.IsOwner(owner));
        Assert.True(organization.IsAdmin(owner));
        Assert.Single(organization.Members);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(100)]
    public void AddMember_InvalidRole_Throws(int role)
    {
        var organization = NewOrganization(NewUser());

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            organization.AddMember(NewUser(), (OrganizationRole)role));

        Assert.Single(organization.Members);
    }

    [Fact]
    public void Restore_WithoutOwnerMembership_Throws()
    {
        var owner = NewUser();

        Assert.Throws<OrganizationRuleViolationException>(() =>
            Organization.Restore(
                new OrganizationKey("Acme"),
                owner,
                DateTimeOffset.UtcNow,
                []));
    }

    [Fact]
    public void Restore_WithOwnerNotAdmin_Throws()
    {
        var owner = NewUser();

        var membership = OrganizationMember.Restore(
            owner,
            OrganizationRole.Member,
            DateTimeOffset.UtcNow);

        Assert.Throws<OrganizationRuleViolationException>(() =>
            Organization.Restore(
                new OrganizationKey("Acme"),
                owner,
                DateTimeOffset.UtcNow,
                [membership]));
    }

    [Fact]
    public void Restore_DuplicateMemberships_Throws()
    {
        var owner = NewUser();
        var joinedAt = DateTimeOffset.UtcNow;

        var first = OrganizationMember.Restore(
            owner, OrganizationRole.Admin, joinedAt);

        var duplicate = OrganizationMember.Restore(
            new UserKey(owner.Id, "Renamed"),
            OrganizationRole.Admin,
            joinedAt);

        Assert.Throws<OrganizationRuleViolationException>(() =>
            Organization.Restore(
                new OrganizationKey("Acme"),
                owner,
                joinedAt,
                [first, duplicate]));
    }
}
