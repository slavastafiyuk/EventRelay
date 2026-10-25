namespace EventRelay.ControlPlaneService.Presentation.Contracts.User;

public sealed record UserDTO
{
    public required UserKeyDTO Key { get; init; }
    public required string Issuer { get; init; }
    public required string Subject { get; init; }
}
