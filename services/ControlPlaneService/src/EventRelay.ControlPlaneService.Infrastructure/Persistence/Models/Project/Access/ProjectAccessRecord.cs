using EventRelay.ControlPlaneService.Domain.Entities.Project.Access;

namespace EventRelay.ControlPlaneService.Infrastructure.Persistence.Models.Project.Access;

public sealed record ProjectAccessRecord
{
    public Guid ProjectId { get; set; }
    public ProjectVisibility Visibility { get; set; } = ProjectVisibility.Private;

    public List<ProjectMemberRecord> Members { get; set; } = new();
}
