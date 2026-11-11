using System.ComponentModel.DataAnnotations;

namespace EventRelay.ControlPlaneService.Presentation.Contracts.Api;

public sealed record CreateApiKeyDTO
{
    [Required]
    [StringLength(150, MinimumLength = 1)]
    public required string Name { get; init; }
}
