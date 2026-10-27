using EventRelay.ControlPlaneService.Domain.Entities.Project;
using EventRelay.ControlPlaneService.Domain.Entities.Project.Access;
using EventRelay.ControlPlaneService.Domain.Entities.User;
using EventRelay.ControlPlaneService.Infrastructure.Persistence.Models.Project;
using EventRelay.ControlPlaneService.Infrastructure.Persistence.Models.Project.Access;
using EventRelay.ControlPlaneService.Infrastructure.Persistence.Models.User;
using Mapster;

namespace EventRelay.ControlPlaneService.Tests.Unit.Mappings;

[Collection("Mapster mappings")]
public sealed class RepositoryMappingTests
{
    [Fact]
    public void Project_MapsToRecord_WithForeignKeysAndAccess()
    {
        var original = CreateInternalProject();

        var record = original.Adapt<ProjectRecord>();

        Assert.Equal(original.Key.Id, record.Id);
        Assert.Equal(original.Key.Name, record.Name);
        Assert.Equal(original.Description, record.Description);
        Assert.Equal(original.CreatedAt, record.CreatedAt);
        Assert.Equal(original.Owner.Id, record.OwnerId);
        Assert.Null(record.Owner);
        Assert.Equal(ProjectVisibility.Internal, record.Access.Visibility);

        var originalMember = Assert.Single(original.Access.Members);
        var recordMember = Assert.Single(record.Access.Members);

        Assert.Equal(originalMember.User.Id, recordMember.UserId);
        Assert.Equal(originalMember.Role, recordMember.Role);
        Assert.Null(recordMember.User);
    }

    [Fact]
    public void ProjectRecord_WithLoadedNavigations_RestoresDomainValues()
    {
        var original = CreateInternalProject();
        var record = original.Adapt<ProjectRecord>();
        record.Owner = original.Owner.Adapt<UserKeyRecord>();
        var originalMember = Assert.Single(original.Access.Members);
        var recordMember = Assert.Single(record.Access.Members);
        recordMember.User = originalMember.User.Adapt<UserKeyRecord>();
        var restored = record.Adapt<Project>();

        Assert.NotSame(original, restored);
        Assert.Equal(original.Key.Id, restored.Key.Id);
        Assert.Equal(original.Key.Name, restored.Key.Name);
        Assert.Equal(original.Owner.Id, restored.Owner.Id);
        Assert.Equal(original.Owner.Name, restored.Owner.Name);
        Assert.Equal(original.Description, restored.Description);
        Assert.Equal(original.CreatedAt, restored.CreatedAt);
        Assert.Equal(original.Access.Visibility, restored.Access.Visibility);

        var restoredMember = Assert.Single(restored.Access.Members);

        Assert.Equal(originalMember.User.Id, restoredMember.User.Id);
        Assert.Equal(originalMember.User.Name, restoredMember.User.Name);
        Assert.Equal(originalMember.Role, restoredMember.Role);
    }

    [Fact]
    public void User_RoundTrip_PreservesIdentityAndAuthenticationDetails()
    {
        var original = new User(
            new UserKey(Guid.CreateVersion7(), "owner"),
            "https://identity.example.test/realms/eventrelay",
            "user-123");

        var record = original.Adapt<UserRecord>();
        var restored = record.Adapt<User>();

        Assert.Equal(original.Key.Id, record.Id);
        Assert.Equal(original.Key.Name, record.Name);
        Assert.Equal(original.Issuer, record.Issuer);
        Assert.Equal(original.Subject, record.Subject);

        Assert.Equal(original.Key.Id, restored.Key.Id);
        Assert.Equal(original.Key.Name, restored.Key.Name);
        Assert.Equal(original.Issuer, restored.Issuer);
        Assert.Equal(original.Subject, restored.Subject);
    }

    [Fact]
    public void ProjectRecord_WithOwnerDuplicatedInMembers_CannotRestore()
    {
        var ownerId = Guid.CreateVersion7();

        var record = new ProjectRecord
        {
            Id = Guid.CreateVersion7(),
            Name = "Invalid project",
            Description = "Invalid membership",
            CreatedAt = DateTime.UtcNow,
            Owner = new UserKeyRecord
            {
                Id = ownerId,
                Name = "owner"
            },
            Access = new ProjectAccessRecord
            {
                Visibility = ProjectVisibility.Internal,
                Members =
                [
                    new ProjectMemberRecord
                    {
                        User = new UserKeyRecord
                        {
                            Id = ownerId,
                            Name = "owner"
                        },
                        Role = ProjectRole.Editor
                    }
                ]
            }
        };

        Assert.Throws<InvalidOperationException>(
            () => record.Adapt<Project>());
    }

    private static Project CreateInternalProject()
    {
        var project = new Project(
            new ProjectKey(Guid.CreateVersion7(), "MyStore"),
            new UserKey(Guid.CreateVersion7(), "owner"),
            new DateTime(2026, 10, 20, 12, 30, 0, DateTimeKind.Utc))
        {
            Description = "Online store"
        };

        project.ChangeVisibility(ProjectVisibility.Internal);

        project.AddMember(
            new UserKey(Guid.CreateVersion7(), "editor"),
            ProjectRole.Editor);

        return project;
    }
}
