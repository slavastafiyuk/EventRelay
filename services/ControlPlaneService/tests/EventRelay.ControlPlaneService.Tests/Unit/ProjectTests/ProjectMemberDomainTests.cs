
using EventRelay.ControlPlaneService.Domain.Entities.Project;
using EventRelay.ControlPlaneService.Domain.Entities.Project.Access;
using EventRelay.ControlPlaneService.Domain.Entities.User;

namespace EventRelay.ControlPlaneService.Tests.Unit.ProjectTests;

public sealed class ProjectMemberDomainTests
{
    private static Project NewProject(UserKey owner) =>
        new(new ProjectKey($"Members-{Guid.NewGuid():N}"), owner);

    private static UserKey NewUser() =>
        new(Guid.CreateVersion7(), $"user-{Guid.NewGuid():N}");

    [Fact]
    public void AddMember_InternalProject_GrantsPermissionsAccordingToRole()
    {
        var owner = NewUser();
        var viewer = NewUser();
        var editor = NewUser();

        var project = NewProject(owner);
        project.ChangeVisibility(ProjectVisibility.Internal);

        project.AddMember(viewer, ProjectRole.Viewer);
        project.AddMember(editor, ProjectRole.Editor);

        Assert.Equal(2, project.Access.Members.Count);

        Assert.True(project.HasPermission(
            viewer, ProjectPermission.Read));

        Assert.False(project.HasPermission(
            viewer, ProjectPermission.Update));

        Assert.True(project.HasPermission(
            editor, ProjectPermission.Read));

        Assert.True(project.HasPermission(
            editor, ProjectPermission.Update));

        Assert.False(project.HasPermission(
            editor, ProjectPermission.ManageMembers));
    }

    [Theory]
    [InlineData(ProjectVisibility.Private)]
    [InlineData(ProjectVisibility.Public)]
    public void AddMember_NonInternalProject_Throws(
        ProjectVisibility visibility)
    {
        var project = NewProject(NewUser());
        project.ChangeVisibility(visibility);

        Assert.Throws<InvalidOperationException>(() =>
            project.AddMember(NewUser(), ProjectRole.Viewer));

        Assert.Empty(project.Access.Members);
    }

    [Fact]
    public void AddMember_SameUserTwice_Throws()
    {
        var project = NewProject(NewUser());
        var member = NewUser();

        project.ChangeVisibility(ProjectVisibility.Internal);
        project.AddMember(member, ProjectRole.Viewer);

        Assert.Throws<InvalidOperationException>(() =>
            project.AddMember(member, ProjectRole.Editor));

        Assert.Single(project.Access.Members);

        Assert.Equal(
            ProjectRole.Viewer,
            project.Access.Members.Single().Role);
    }

    [Fact]
    public void AddMember_Owner_Throws()
    {
        var owner = NewUser();
        var project = NewProject(owner);

        project.ChangeVisibility(ProjectVisibility.Internal);

        Assert.Throws<InvalidOperationException>(() =>
            project.AddMember(owner, ProjectRole.Editor));

        Assert.Empty(project.Access.Members);
    }

    [Fact]
    public void RemoveMember_RemovesTheirPermissions()
    {
        var project = NewProject(NewUser());
        var member = NewUser();

        project.ChangeVisibility(ProjectVisibility.Internal);
        project.AddMember(member, ProjectRole.Editor);

        project.RemoveMember(member);

        Assert.Empty(project.Access.Members);

        Assert.False(project.HasPermission(
            member, ProjectPermission.Read));

        Assert.False(project.HasPermission(
            member, ProjectPermission.Update));

        Assert.Throws<InvalidOperationException>(() =>
            project.RemoveMember(member));
    }

    [Theory]
    [InlineData(ProjectVisibility.Private)]
    [InlineData(ProjectVisibility.Public)]
    public void ChangeVisibility_AwayFromInternal_ClearsMembers(
        ProjectVisibility visibility)
    {
        var project = NewProject(NewUser());

        project.ChangeVisibility(ProjectVisibility.Internal);
        project.AddMember(NewUser(), ProjectRole.Viewer);

        project.ChangeVisibility(visibility);

        Assert.Equal(visibility, project.Access.Visibility);
        Assert.Empty(project.Access.Members);
    }
}
