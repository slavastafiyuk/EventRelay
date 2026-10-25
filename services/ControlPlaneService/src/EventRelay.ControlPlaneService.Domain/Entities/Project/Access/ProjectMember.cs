using EventRelay.ControlPlaneService.Domain.Entities.User;

namespace EventRelay.ControlPlaneService.Domain.Entities.Project.Access;

public sealed class ProjectMember
{
    public UserKey User { get; private set; } = null!;
    public ProjectRole Role { get; private set; } = ProjectRole.Viewer;
    private ProjectMember() { }
    public ProjectMember(UserKey user, ProjectRole role)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentOutOfRangeException.ThrowIfNotEqual(Enum.IsDefined(role), true);

        User = user;
        Role = role;
    }
}
