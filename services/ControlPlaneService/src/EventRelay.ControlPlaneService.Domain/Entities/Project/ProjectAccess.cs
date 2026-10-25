using EventRelay.ControlPlaneService.Domain.Entities.User;

namespace EventRelay.ControlPlaneService.Domain.Entities.Project;

public sealed class ProjectAccess
{
    public ProjectVisibility Visibility { get; set; } = ProjectVisibility.Private;
    public List<UserKey>? Users { get; set; } = new();
}

