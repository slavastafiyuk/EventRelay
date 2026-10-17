namespace EventRelay.ControlPlaneService.Infrastructure.Persistence.Models;

public sealed record ProjectRecord
{
    public Guid Id { get; init; }
    public string Name { get; init; } = null!;
    public string? Description { get; set; }
}
