namespace EventRelay.ControlPlaneService.Presentation.Contracts.Api;

public sealed record IssuedApiDTO
{
    public required ProjectApiDTO Api { get; init; }
    public required string Secret { get; init; }
}
