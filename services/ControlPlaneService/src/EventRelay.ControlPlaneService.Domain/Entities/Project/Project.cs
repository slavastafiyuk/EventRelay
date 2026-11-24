using CommunityToolkit.Diagnostics;
using EventRelay.ControlPlaneService.Domain.Entities.Organization;
using EventRelay.ControlPlaneService.Domain.Entities.Project.Access;
using EventRelay.ControlPlaneService.Domain.Entities.User;
using EventRelay.ControlPlaneService.Domain.Exceptions.Project;

namespace EventRelay.ControlPlaneService.Domain.Entities.Project;

public class Project
{
    public ProjectKey Key { get; private set; } = null!;
    public OrganizationKey? Organization { get; private set; }
    public UserKey Owner { get; private set; } = null!;
    public string? Description { get; set; }
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    public ProjectAccess Access { get; private set; } = new();
    private const ProjectPermission DefinedPermissions =
        ProjectPermission.Read |
        ProjectPermission.Update |
        ProjectPermission.Delete |
        ProjectPermission.ManageMembers |
        ProjectPermission.ManageApiKeys;

    public ProjectMember AddMember(UserKey user, ProjectRole role)
    {
        Guard.IsNotNull(user);

        if (user.Equals(Owner))
            throw new ProjectMemberConflictException("Project owner cannot be added as a member.");

        return Access.AddMember(user, role);
    }

    public void RemoveMember(UserKey user) => Access.RemoveMember(user);

    public void ChangeVisibility(ProjectVisibility visibility) => Access.SetVisibility(visibility);

    public bool HasPermission(UserKey user, ProjectPermission required)
    {
        ArgumentNullException.ThrowIfNull(user);

        if (required == ProjectPermission.None || (required & ~DefinedPermissions) != ProjectPermission.None)
            return false;

        if (Owner.Equals(user))
            return true;

        if (required == ProjectPermission.Read && Access.Visibility == ProjectVisibility.Public)
            return true;

        if (Access.Visibility != ProjectVisibility.Internal)
            return false;

        var member = Access.Members.FirstOrDefault(m => m.User.Equals(user));

        var granted = member?.Role switch
        {
            ProjectRole.Viewer => ProjectPermission.Read,
            ProjectRole.Editor => ProjectPermission.Read | ProjectPermission.Update,
            _ => ProjectPermission.None
        };

        return (granted & required) == required;
    }

    public ProjectMember ChangeMemberRole(UserKey user, ProjectRole role)
        => Access.ChangeMemberRole(user, role);

    public static Project Restore(
        ProjectKey key,
        UserKey owner,
        DateTime createdAt,
        string? description,
        ProjectAccess access,
        OrganizationKey? organization = null)
    {
        ArgumentNullException.ThrowIfNull(access);

        var project = new Project(key, owner, createdAt)
        {
            Description = description,
            Access = access,
            Organization = organization
        };

        if (access.Members.Any(m => m.User.Equals(owner)))
        {
            throw new InvalidOperationException(
                "Project owner cannot be a member.");
        }

        return project;
    }

    public void RefreshOrganizationKey(OrganizationKey validatedKey)
    {
        ArgumentNullException.ThrowIfNull(validatedKey);

        if (Organization is not null && Organization.Id != validatedKey.Id)
            throw new InvalidOperationException("Cannot change the project's organization.");

        Organization = validatedKey;
    }

    private Project() { }

    public Project(ProjectKey key, UserKey owner)
    {
        Key = key ?? throw new ArgumentNullException(nameof(key));
        Owner = owner ?? throw new ArgumentNullException(nameof(owner));
    }

    public Project(
    ProjectKey key,
    UserKey owner,
    OrganizationKey organization)
    : this(key, owner)
    {
        Organization = organization ?? throw new ArgumentNullException(nameof(organization));
    }

    public Project(
        ProjectKey key,
        UserKey owner,
        DateTime createdAt)
        : this(key, owner)
    {
        CreatedAt = createdAt;
    }
}
