namespace EventRelay.ControlPlaneService.Domain.Entities.User;

public class UserKey : KeyBase<UserKey>
{
    public string Name { get; set; } = null!;
    private UserKey() { }
    public UserKey(string name) => Name = name;
    public UserKey(Guid id, string name) : base(id) => Name = name;
}
