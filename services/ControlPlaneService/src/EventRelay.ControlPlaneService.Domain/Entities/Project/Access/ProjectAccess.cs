using EventRelay.ControlPlaneService.Domain.Entities.User;
using EventRelay.ControlPlaneService.Domain.Exceptions.Project;

namespace EventRelay.ControlPlaneService.Domain.Entities.Project.Access;

public sealed class ProjectAccess
{
    private readonly List<ProjectMember> _members = [];
    public ProjectVisibility Visibility { get; private set; } = ProjectVisibility.Private;
    public IReadOnlyCollection<ProjectMember> Members => _members.AsReadOnly();

    internal ProjectMember AddMember(UserKey user, ProjectRole role)
    {
        ArgumentNullException.ThrowIfNull(user);

        if (Visibility != ProjectVisibility.Internal)
            throw new InvalidOperationException("Only internal projects can have members.");

        if (!Enum.IsDefined(role))
            throw new ArgumentOutOfRangeException(nameof(role));

        if (_members.Any(x => x.User.Equals(user)))
            throw new InvalidOperationException("User already belongs to this project.");

        var member = new ProjectMember(user, role);

        _members.Add(member);

        return member;
    }

    internal ProjectMember ChangeMemberRole(UserKey user, ProjectRole role)
    {
        ArgumentNullException.ThrowIfNull(user);

        var member = _members.SingleOrDefault(x => x.User.Equals(user))
            ?? throw new ProjectMemberConflictException("User is not a project member.");
        member.ChangeRole(role);

        return member;
    }

    internal void RemoveMember(UserKey user)
    {
        var removed = _members.RemoveAll(x => x.User.Equals(user));

        if (removed == 0) throw new InvalidOperationException("User is not a project member.");
    }

    internal void SetVisibility(ProjectVisibility visibility)
    {
        if (!Enum.IsDefined(visibility))
            throw new ArgumentOutOfRangeException(nameof(visibility));

        Visibility = visibility;

        if (visibility is ProjectVisibility.Private or ProjectVisibility.Public) _members.Clear();
    }

    public static ProjectAccess Restore(
        ProjectVisibility visibility,
        IEnumerable<ProjectMember>? members = null)
    {
        var access = new ProjectAccess();
        access.SetVisibility(visibility);
        var restoredMembers = members?.ToList() ?? [];

        if (visibility != ProjectVisibility.Internal && restoredMembers.Count > 0)
            throw new InvalidOperationException("Only internal projects can have members.");

        foreach (var member in restoredMembers)
        {
            ArgumentNullException.ThrowIfNull(member);
            access.AddMember(member.User, member.Role);
        }

        return access;
    }
}

