namespace EventRelay.ControlPlaneService.Presentation.Contracts.User;

public sealed record UserKeyDTO
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
}
