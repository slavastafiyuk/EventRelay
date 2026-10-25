namespace EventRelay.ControlPlaneService.Infrastructure.Persistence.Models.User;

public sealed record UserRecord
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string Issuer { get; set; } = null!;
    public string Subject { get; set; } = null!;
}
