using System.ComponentModel.DataAnnotations;

namespace EventRelay.ControlPlaneService.Presentation.Contracts.Organization;

public sealed record OrganizationCreateDTO
{
    [Required]
    [StringLength(150, MinimumLength = 1)]
    public required string Name { get; init; }
}
