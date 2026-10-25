using EventRelay.ControlPlaneService.Domain.Entities.User;

namespace EventRelay.ControlPlaneService.Domain.Entities.Project.Access;

public sealed class ProjectMember
{
    public UserKey UserKey { get; private set; } = null!;
    public ProjectRole Role { get; private set; } = ProjectRole.Viewer;
    private ProjectMember() { }
    public ProjectMember(UserKey userKey, ProjectRole role)
    {
        UserKey = userKey ?? throw new ArgumentNullException(nameof(userKey));
        Role = role;
    }
}
