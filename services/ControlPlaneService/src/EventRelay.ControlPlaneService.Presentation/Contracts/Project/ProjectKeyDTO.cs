namespace EventRelay.ControlPlaneService.Presentation.Contracts.Project;

public sealed record ProjectKeyDTO
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
}
