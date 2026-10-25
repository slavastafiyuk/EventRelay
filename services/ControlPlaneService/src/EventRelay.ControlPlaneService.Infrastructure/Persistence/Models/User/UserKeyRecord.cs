namespace EventRelay.ControlPlaneService.Infrastructure.Persistence.Models.User;

public sealed record UserKeyRecord
{
    public Guid Id { get; init; }
    public string Name { get; init; } = null!;
}
