using EventRelay.ControlPlaneService.Domain.Entities.Organization.Access;
using EventRelay.ControlPlaneService.Infrastructure.Persistence.Models.User;

namespace EventRelay.ControlPlaneService.Infrastructure.Persistence.Models.Organization;

public sealed record OrganizationMemberRecord
{
    public Guid OrganizationId { get; set; }
    public Guid UserId { get; set; }
    public UserKeyRecord User { get; set; } = null!;
    public OrganizationRole Role { get; set; }
    public DateTimeOffset JoinedAt { get; set; }
}
