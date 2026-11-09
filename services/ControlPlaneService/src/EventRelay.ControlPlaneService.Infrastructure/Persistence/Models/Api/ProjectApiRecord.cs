using EventRelay.ControlPlaneService.Infrastructure.Persistence.Models.Project;
using EventRelay.ControlPlaneService.Infrastructure.Persistence.Models.User;

namespace EventRelay.ControlPlaneService.Infrastructure.Persistence.Models.Api;


public sealed class ProjectApiRecord
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string KeyHash { get; set; } = null!;
    public Guid ProjectId { get; set; }
    public ProjectKeyRecord Project { get; set; } = null!;
    public Guid CreatedByUserId { get; set; }
    public UserKeyRecord CreatedBy { get; set; } = null!;
    public Guid? RevokedByUserId { get; set; }
    public UserKeyRecord? RevokedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
}

