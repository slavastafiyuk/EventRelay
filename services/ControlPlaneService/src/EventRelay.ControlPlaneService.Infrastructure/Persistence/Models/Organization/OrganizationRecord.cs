using EventRelay.ControlPlaneService.Infrastructure.Persistence.Models.User;

namespace EventRelay.ControlPlaneService.Infrastructure.Persistence.Models.Organization;

public sealed class OrganizationRecord
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public DateTimeOffset CreatedAt { get; set; }
    public Guid OwnerId { get; set; }
    public UserKeyRecord Owner { get; set; } = null!;
    public List<OrganizationMemberRecord> Members { get; set; } = [];
}
