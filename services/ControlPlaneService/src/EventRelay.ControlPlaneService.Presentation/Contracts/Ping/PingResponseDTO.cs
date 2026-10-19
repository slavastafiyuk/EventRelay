namespace EventRelay.ControlPlaneService.Presentation.Contracts.Ping;

public sealed record PingResponseDTO
{
    public required string Service { get; init; }
    public required string Status { get; init; }
}
