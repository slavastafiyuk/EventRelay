namespace EventRelay.ControlPlaneService.Presentation.Contracts.Api;

public sealed record CreateApiKeyDTO
{
    public required string Name { get; init; }
}
