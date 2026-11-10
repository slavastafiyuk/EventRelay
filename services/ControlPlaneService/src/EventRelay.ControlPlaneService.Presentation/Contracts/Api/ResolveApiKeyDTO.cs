using System.ComponentModel.DataAnnotations;

namespace EventRelay.ControlPlaneService.Presentation.Contracts.Api;

public sealed record ResolveApiKeyDTO
{
    [property: Required]
    [RegularExpression(@"^[a-fA-F0-9]{64}$", ErrorMessage = "KeyHash must be a valid SHA-256 hexadecimal hash.")]
    public required string KeyHash { get; init; }
}
