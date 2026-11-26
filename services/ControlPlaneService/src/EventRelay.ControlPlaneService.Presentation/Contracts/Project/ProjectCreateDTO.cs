using System.ComponentModel.DataAnnotations;
using EventRelay.ControlPlaneService.Presentation.Contracts.Organization;

namespace EventRelay.ControlPlaneService.Presentation.Contracts.Project;

public sealed record ProjectCreateDTO
{
    [property: Required]
    public required OrganizationKeyDTO Organization { get; init; }

    [property: Required]
    [property: StringLength(150)]
    public required string Name { get; init; }

    [property: StringLength(500)]
    public string? Description { get; init; }
}
