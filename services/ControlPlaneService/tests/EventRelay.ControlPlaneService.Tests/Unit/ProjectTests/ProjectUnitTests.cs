using EventRelay.ControlPlaneService.Domain.Entities.Project;
using EventRelay.ControlPlaneService.Domain.Entities.Project.Access;
using EventRelay.ControlPlaneService.Domain.Entities.User;

namespace EventRelay.ControlPlaneService.Tests.Unit.ProjectTests;

public sealed class ProjectUnitTests
{
    [Fact]
    public void CanRead_RespectsProjectVisibility()
    {
        var owner = new UserKey(Guid.NewGuid(), "owner");
        var member = new UserKey(Guid.NewGuid(), "member");
        var outsider = new UserKey(Guid.NewGuid(), "outsider");
        var project = new Project(new ProjectKey("MyProject"), owner);

        Assert.True(project.CanRead(owner));
        Assert.False(project.CanRead(member));
        Assert.False(project.CanRead(outsider));

        project.ChangeVisibility(ProjectVisibility.Internal);
        project.AddMember(member, ProjectRole.Viewer);

        Assert.True(project.CanRead(owner));
        Assert.True(project.CanRead(member));
        Assert.False(project.CanRead(outsider));

        project.ChangeVisibility(ProjectVisibility.Public);

        Assert.True(project.CanRead(owner));
        Assert.True(project.CanRead(member));
        Assert.True(project.CanRead(outsider));
    }
}
