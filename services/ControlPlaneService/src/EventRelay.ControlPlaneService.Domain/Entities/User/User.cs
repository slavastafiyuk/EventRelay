namespace EventRelay.ControlPlaneService.Domain.Entities.User;

public class User
{
    public UserKey Key { get; set; } = null!;
    public int? Age { get; set; }
}
