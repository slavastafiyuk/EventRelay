namespace EventRelay.ControlPlaneService.Presentation.Contracts.Api;

public sealed record ApiKeyDTO
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
}
