
using EventRelay.ControlPlaneService.Domain.Entities.Project;
using EventRelay.ControlPlaneService.Domain.Entities.Project.Access;
using EventRelay.ControlPlaneService.Domain.Entities.User;

namespace EventRelay.ControlPlaneService.Tests.Unit.ProjectTests;

public sealed class ProjectPermissionValidationTests
{
    private static UserKey NewUser() =>
        new(Guid.CreateVersion7(), "Test user");

    private static Project NewProject(UserKey owner) =>
        new(
            new ProjectKey($"Test-{Guid.NewGuid():N}"),
            owner);

    [Theory]
    [InlineData(ProjectPermission.Read)]
    [InlineData(ProjectPermission.Update)]
    [InlineData(ProjectPermission.Delete)]
    [InlineData(ProjectPermission.ManageMembers)]
    [InlineData(ProjectPermission.ManageApiKeys)]
    public void Owner_HasEveryDefinedPermission(
        ProjectPermission permission)
    {
        var owner = NewUser();
        var project = NewProject(owner);

        Assert.True(project.HasPermission(owner, permission));
    }

    [Fact]
    public void Owner_CannotUseUndefinedPermissions()
    {
        var owner = NewUser();
        var project = NewProject(owner);

        var unsupported = (ProjectPermission)32;

        Assert.False(project.HasPermission(
            owner, unsupported));

        Assert.False(project.HasPermission(
            owner, ProjectPermission.None));
    }

    [Fact]
    public void Viewer_CanReadButCannotManageMembers()
    {
        var owner = NewUser();
        var viewer = NewUser();

        var project = NewProject(owner);

        project.ChangeVisibility(ProjectVisibility.Internal);
        project.AddMember(viewer, ProjectRole.Viewer);

        Assert.True(project.HasPermission(
            viewer, ProjectPermission.Read));

        Assert.False(project.HasPermission(
            viewer, ProjectPermission.ManageMembers));
    }

    [Fact]
    public void Editor_CanUpdateButCannotManageMembers()
    {
        var owner = NewUser();
        var editor = NewUser();

        var project = NewProject(owner);

        project.ChangeVisibility(ProjectVisibility.Internal);
        project.AddMember(editor, ProjectRole.Editor);

        Assert.True(project.HasPermission(
            editor, ProjectPermission.Update));

        Assert.False(project.HasPermission(
            editor, ProjectPermission.ManageMembers));
    }
}
