using EventRelay.ControlPlaneService.Domain.Entities.User;

namespace EventRelay.ControlPlaneService.Domain.Entities.Project.Access;

public sealed class ProjectAccess
{
    private readonly List<ProjectMember> _members = [];
    public ProjectVisibility Visibility { get; private set; } = ProjectVisibility.Private;
    public IReadOnlyCollection<ProjectMember> Members => _members.AsReadOnly();

    internal void AddMember(UserKey user, ProjectRole role)
    {
        ArgumentNullException.ThrowIfNull(user);

        if (Visibility != ProjectVisibility.Internal)
            throw new InvalidOperationException("Only internal projects can have members.");

        if (!Enum.IsDefined(role))
            throw new ArgumentOutOfRangeException(nameof(role));

        if (_members.Any(x => x.UserKey.Equals(user)))
            throw new InvalidOperationException("User already belongs to this project.");

        _members.Add(new ProjectMember(user, role));
    }

    internal void RemoveMember(UserKey user)
    {
        var removed = _members.RemoveAll(x => x.UserKey.Equals(user));

        if (removed == 0) throw new InvalidOperationException("User is not a project member.");
    }

    internal void SetVisibility(ProjectVisibility visibility)
    {
        if (!Enum.IsDefined(visibility) || visibility == ProjectVisibility.Public)
            throw new InvalidOperationException("This visibility is not supported yet.");

        Visibility = visibility;

        if (visibility == ProjectVisibility.Private) _members.Clear();
    }
}

