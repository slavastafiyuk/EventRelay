using System.ComponentModel.DataAnnotations;

namespace EventRelay.ControlPlaneService.Presentation.Contracts.User;

public sealed record UserCreateDTO
{
    [property: Required]
    [property: StringLength(150)]
    public required string Issuer { get; init; }
    [property: Required]
    public required string Subject { get; init; }
    [property: Required]
    public required string Name { get; init; }
}
