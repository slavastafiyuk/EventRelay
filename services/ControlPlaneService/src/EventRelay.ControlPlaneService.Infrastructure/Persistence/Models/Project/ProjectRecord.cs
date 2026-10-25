using EventRelay.ControlPlaneService.Infrastructure.Persistence.Models.Project.Access;
using EventRelay.ControlPlaneService.Infrastructure.Persistence.Models.User;

namespace EventRelay.ControlPlaneService.Infrastructure.Persistence.Models.Project;

public sealed class ProjectRecord
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }

    public UserKeyRecord Owner { get; set; } = null!;

    public ProjectAccessRecord Access { get; set; } = null!;
}
