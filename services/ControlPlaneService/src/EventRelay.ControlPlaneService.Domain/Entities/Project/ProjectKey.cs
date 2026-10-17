namespace EventRelay.ControlPlaneService.Domain.Entities.Project;

public class ProjectKey : KeyBase<ProjectKey, string>
{
    public string Name
    {
        get;
        set => field = string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("Invalid project name.")
            : value.Trim();
    } = null!;

    public override string SecondaryKey => Name;

    private ProjectKey() { }

    public ProjectKey(string name) => Name = name;

    public ProjectKey(Guid id, string name) : base(id) => Name = name;
}
