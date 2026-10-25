using EventRelay.ControlPlaneService.Domain.Entities.User;

namespace EventRelay.ControlPlaneService.Domain.Entities.Project;

public class Project
{
    public ProjectKey Key { get; private set; } = null!;
    public string? Description { get; set; }
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    public UserKey Owner { get; private set; } = null!;
    public ProjectAccess Access { get; set; } = new();

    private Project() { }
    public Project(ProjectKey key) => Key = key ?? throw new ArgumentNullException(nameof(key));
}
