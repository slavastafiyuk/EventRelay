namespace EventRelay.ProjectService.Domain.Entities.Project;

public class Project
{
    public ProjectKey Key { get; private set; } = null!;
    public string? Description { get; set; }

    private Project() { }

    public Project(ProjectKey key) => Key = key ?? throw new ArgumentNullException(nameof(key));
}
