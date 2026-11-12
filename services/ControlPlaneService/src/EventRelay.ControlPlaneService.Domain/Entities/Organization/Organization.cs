
using EventRelay.ControlPlaneService.Domain.Entities.Organization.Access;
using EventRelay.ControlPlaneService.Domain.Entities.User;
using EventRelay.ControlPlaneService.Domain.Exceptions.Organization;

namespace EventRelay.ControlPlaneService.Domain.Entities.Organization;

public sealed class Organization
{
    public OrganizationKey Key { get; private set; }
    public UserKey Owner { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    private readonly List<OrganizationMember> _members = [];
    public IReadOnlyCollection<OrganizationMember> Members => _members.AsReadOnly();

    #region Constructors

    public Organization(OrganizationKey key, UserKey owner)
    {
        Key = key ?? throw new ArgumentNullException(nameof(key));
        Owner = owner ?? throw new ArgumentNullException(nameof(owner));
        CreatedAt = DateTimeOffset.UtcNow;

        AddMember(owner, OrganizationRole.Admin);
    }

    private Organization(
        OrganizationKey key,
        UserKey owner,
        DateTimeOffset createdAt,
        IEnumerable<OrganizationMember> members)
    {
        Key = key ?? throw new ArgumentNullException(nameof(key));
        Owner = owner ?? throw new ArgumentNullException(nameof(owner));

        if (createdAt == default)
            throw new ArgumentException("CreatedAt cannot be empty.", nameof(createdAt));

        ArgumentNullException.ThrowIfNull(members);

        var restoredMembers = members.ToList();

        if (restoredMembers.Any(m => m is null))
            throw new OrganizationRuleViolationException("Organization members cannot contain null.");

        if (restoredMembers
            .GroupBy(m => m.User.Id)
            .Any(group => group.Count() > 1))
        {
            throw new OrganizationRuleViolationException("User is already a member of the organization.");
        }

        var ownerMember = restoredMembers.FirstOrDefault(m => m.User.Equals(owner));

        if (ownerMember is null || ownerMember.Role != OrganizationRole.Admin)
        {
            throw new OrganizationRuleViolationException("Organization owner must be an Admin member.");
        }

        CreatedAt = createdAt;
        _members.AddRange(restoredMembers);
    }

    #endregion

    #region Member Management

    public void AddMember(UserKey user, OrganizationRole role)
    {
        ArgumentNullException.ThrowIfNull(user);

        if (!Enum.IsDefined(role))
            throw new ArgumentOutOfRangeException(nameof(role));

        if (IsMember(user))
            throw new OrganizationRuleViolationException("User is already a member of the organization.");

        _members.Add(new OrganizationMember(user, role));
    }

    public void RemoveMember(UserKey user)
    {
        ArgumentNullException.ThrowIfNull(user);

        if (IsOwner(user))
            throw new OrganizationRuleViolationException("Organization owner cannot be removed.");

        var member = FindMember(user) ??
            throw new OrganizationRuleViolationException("User is not a member of the organization.");

        _members.Remove(member);
    }

    public void ChangeMemberRole(UserKey user, OrganizationRole role)
    {
        ArgumentNullException.ThrowIfNull(user);

        if (!Enum.IsDefined(role))
            throw new ArgumentOutOfRangeException(nameof(role));

        if (IsOwner(user) && role != OrganizationRole.Admin)
            throw new OrganizationRuleViolationException("Organization owner must remain an Admin.");

        var member = FindMember(user)
            ?? throw new OrganizationRuleViolationException("User is not a member of the organization.");

        member.ChangeRole(role);
    }

    #endregion

    #region Membership Queries

    public bool IsOwner(UserKey user)
    {
        ArgumentNullException.ThrowIfNull(user);

        return Owner.Equals(user);
    }

    public bool IsMember(UserKey user)
    {
        ArgumentNullException.ThrowIfNull(user);

        return FindMember(user) is not null;
    }

    public bool IsAdmin(UserKey user)
    {
        ArgumentNullException.ThrowIfNull(user);

        var member = FindMember(user);

        return member?.Role == OrganizationRole.Admin;
    }

    #endregion

    #region Persistence

    public static Organization Restore(
        OrganizationKey key,
        UserKey owner,
        DateTimeOffset createdAt,
        IEnumerable<OrganizationMember> members)
    {
        return new Organization(
            key,
            owner,
            createdAt,
            members);
    }

    #endregion

    #region Private Helpers

    private OrganizationMember? FindMember(UserKey user)
        => _members.FirstOrDefault(m => m.User.Equals(user));


    #endregion
}
