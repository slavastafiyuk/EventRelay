namespace EventRelay.ControlPlaneService.Infrastructure.Persistence.Models.Organization;

public sealed record OrganizationKeyRecord
{
    public Guid Id { get; init; }
    public string Name { get; init; } = null!;
}
