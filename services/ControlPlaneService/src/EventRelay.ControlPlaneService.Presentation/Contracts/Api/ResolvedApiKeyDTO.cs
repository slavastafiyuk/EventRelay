namespace EventRelay.ControlPlaneService.Presentation.Contracts.Api;

public sealed record ResolvedApiKeyDTO
{
    public required Guid ProjectId { get; init; }
}
