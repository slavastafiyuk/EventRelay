using EventRelay.ControlPlaneService.Domain.Entities.Project.Access;
using EventRelay.ControlPlaneService.Infrastructure.Persistence.Models.User;

namespace EventRelay.ControlPlaneService.Infrastructure.Persistence.Models.Project.Access;

public sealed record ProjectMemberRecord
{
    public UserKeyRecord User { get; set; } = null!;

    public ProjectRole Role { get; set; }
}
