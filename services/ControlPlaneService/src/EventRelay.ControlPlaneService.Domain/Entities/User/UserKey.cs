using EventRelay.ControlPlaneService.Domain.Entities.Project;

namespace EventRelay.ControlPlaneService.Domain.Entities.User;

public class UserKey : KeyBase<ProjectKey>
{
    public string Name { get; set; } = null!;
    private UserKey() { }
    public UserKey(Guid id, string name) : base(id) => Name = name;
}
