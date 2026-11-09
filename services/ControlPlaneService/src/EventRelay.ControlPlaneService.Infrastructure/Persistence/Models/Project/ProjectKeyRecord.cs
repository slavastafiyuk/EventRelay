namespace EventRelay.ControlPlaneService.Infrastructure.Persistence.Models.Project;


public sealed record ProjectKeyRecord
{
    public Guid Id { get; init; }
    public string Name { get; init; } = null!;
}
