using EventRelay.ControlPlaneService.Domain.Entities.Project;
using EventRelay.ControlPlaneService.Domain.Entities.Project.Access;
using EventRelay.ControlPlaneService.Domain.Entities.User;

namespace EventRelay.ControlPlaneService.Tests.Unit.ProjectTests;

public sealed class ProjectUnitTests
{
    [Fact]
    public void HasPermission_Read_RespectsProjectVisibility()
    {
        var owner = new UserKey(Guid.NewGuid(), "owner");
        var member = new UserKey(Guid.NewGuid(), "member");
        var outsider = new UserKey(Guid.NewGuid(), "outsider");
        var project = new Project(new ProjectKey("MyProject"), owner);

        Assert.True(project.HasPermission(owner, ProjectPermission.Read));
        Assert.False(project.HasPermission(member, ProjectPermission.Read));
        Assert.False(project.HasPermission(outsider, ProjectPermission.Read));

        project.ChangeVisibility(ProjectVisibility.Internal);
        project.AddMember(member, ProjectRole.Viewer);

        Assert.True(project.HasPermission(owner, ProjectPermission.Read));
        Assert.True(project.HasPermission(member, ProjectPermission.Read));
        Assert.False(project.HasPermission(outsider, ProjectPermission.Read));

        project.ChangeVisibility(ProjectVisibility.Public);

        Assert.True(project.HasPermission(owner, ProjectPermission.Read));
        Assert.True(project.HasPermission(member, ProjectPermission.Read));
        Assert.True(project.HasPermission(outsider, ProjectPermission.Read));
    }


    [Fact]
    public void ManageApiKeys_IsAllowedOnlyForOwner()
    {
        var owner = new UserKey(Guid.NewGuid(), "owner");
        var editor = new UserKey(Guid.NewGuid(), "editor");
        var viewer = new UserKey(Guid.NewGuid(), "viewer");
        var outsider = new UserKey(Guid.NewGuid(), "outsider");

        var project = new Project(
            new ProjectKey("TestProject"),
            owner);

        project.ChangeVisibility(ProjectVisibility.Internal);

        project.AddMember(editor, ProjectRole.Editor);
        project.AddMember(viewer, ProjectRole.Viewer);

        Assert.True(project.HasPermission(
            owner, ProjectPermission.ManageApiKeys));

        Assert.False(project.HasPermission(
            editor, ProjectPermission.ManageApiKeys));

        Assert.False(project.HasPermission(
            viewer, ProjectPermission.ManageApiKeys));

        Assert.False(project.HasPermission(
            outsider, ProjectPermission.ManageApiKeys));
    }

    [Theory]
    [InlineData(ProjectPermission.Read, true)]
    [InlineData(ProjectPermission.Update, true)]
    [InlineData(ProjectPermission.Delete, true)]
    [InlineData(ProjectPermission.ManageMembers, true)]
    [InlineData(ProjectPermission.ManageApiKeys, true)]
    public void Owner_HasEveryDefinedPermission(ProjectPermission permission, bool expected)
    {
        var owner = new UserKey(Guid.NewGuid(), "owner");

        var project = new Project(
            new ProjectKey("TestProject"),
            owner);

        var actual = project.HasPermission(owner, permission);

        Assert.Equal(expected, actual);
    }

}
