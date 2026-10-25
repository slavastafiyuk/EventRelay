using CommunityToolkit.Diagnostics;
using EventRelay.ControlPlaneService.Domain.Entities.Project.Access;
using EventRelay.ControlPlaneService.Domain.Entities.User;

namespace EventRelay.ControlPlaneService.Domain.Entities.Project;

public class Project
{
    public ProjectKey Key { get; private set; } = null!;
    public string? Description { get; set; }
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    public UserKey Owner { get; private set; } = null!;
    public ProjectAccess Access { get; private set; } = new();

    public void AddMember(UserKey user, ProjectRole role)
    {
        Guard.IsNotNull(user);

        if (user.Equals(Owner))
            throw new InvalidOperationException("Project owner cannot be added as a member.");

        Access.AddMember(user, role);
    }

    public void RemoveMember(UserKey user) => Access.RemoveMember(user);

    public void ChangeVisibility(ProjectVisibility visibility) => Access.SetVisibility(visibility);

    public bool CanRead(UserKey user)
    {
        ArgumentNullException.ThrowIfNull(user);

        if (Owner.Equals(user)) return true;

        if (Access.Visibility == ProjectVisibility.Public) return true;

        return Access.Visibility == ProjectVisibility.Internal
            && Access.Members.Any(member => member.User.Equals(user));
    }

    public static Project Restore(
    ProjectKey key,
    UserKey owner,
    DateTime createdAt,
    string? description,
    ProjectAccess access)
    {
        ArgumentNullException.ThrowIfNull(access);

        var project = new Project(key, owner, createdAt)
        {
            Description = description,
            Access = access
        };

        if (access.Members.Any(m => m.User.Equals(owner)))
            throw new InvalidOperationException(
                "Project owner cannot be a member.");

        return project;
    }

    private Project() { }
    public Project(ProjectKey key, UserKey owner)
    {
        Key = key ?? throw new ArgumentNullException(nameof(key));
        Owner = owner ?? throw new ArgumentNullException(nameof(owner));
    }
    public Project(ProjectKey key, UserKey owner, DateTime createdAt) : this(key, owner)
    {
        CreatedAt = createdAt;
    }
}
