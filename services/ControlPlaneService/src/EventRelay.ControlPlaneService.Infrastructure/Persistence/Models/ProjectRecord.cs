namespace EventRelay.ControlPlaneService.Infrastructure.Persistence.Models;

public sealed record ProjectRecord
{
    public Guid Id { get; init; }
    public Guid OwnerId { get; init; }
    public string OwnerName { get; init; } = null!;
    public string Name { get; init; } = null!;
    public string? Description { get; set; }
}
