
using EventRelay.ControlPlaneService.Domain.Entities.User;

namespace EventRelay.ControlPlaneService.Domain.Entities.Organization.Access;

public sealed class OrganizationMember
{
    public UserKey User { get; private set; }
    public OrganizationRole Role { get; private set; }
    public DateTimeOffset JoinedAt { get; private set; }

    // Normal creation
    internal OrganizationMember(UserKey user, OrganizationRole role)
    {
        User = user ?? throw new ArgumentNullException(nameof(user));

        if (!Enum.IsDefined(role))
            throw new ArgumentOutOfRangeException(nameof(role));

        Role = role;
        JoinedAt = DateTimeOffset.UtcNow;
    }

    public static OrganizationMember Restore(
        UserKey user,
        OrganizationRole role,
        DateTimeOffset joinedAt)
    {
        var member = new OrganizationMember(user, role)
        {
            JoinedAt = joinedAt
        };

        return member;
    }

    internal void ChangeRole(OrganizationRole role)
    {
        if (!Enum.IsDefined(role))
            throw new ArgumentOutOfRangeException(nameof(role));

        Role = role;
    }
}
