namespace EventRelay.ProjectService.Domain.Entities.Project;

public class ProjectKey : KeyBase<ProjectKey, string>
{
    public string Name { get; private set; } = null!;

    public override string SecondaryKey => Name;

    private ProjectKey() { }

    public ProjectKey(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException(
                "Name cannot be null or whitespace.", nameof(name));

        Name = name.Trim();
    }
}
