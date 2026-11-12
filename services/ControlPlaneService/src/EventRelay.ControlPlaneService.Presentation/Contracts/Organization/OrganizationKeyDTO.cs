namespace EventRelay.ControlPlaneService.Presentation.Contracts.Organization;

public sealed record OrganizationKeyDTO
{
    public Guid Id { get; init; }
    public required string Name { get; init; }
}
