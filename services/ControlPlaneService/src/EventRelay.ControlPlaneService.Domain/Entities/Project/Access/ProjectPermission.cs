namespace EventRelay.ControlPlaneService.Domain.Entities.Project.Access;

[Flags]
public enum ProjectPermission
{
    None = 0,
    Read = 1,
    Update = 2,
    Delete = 3,
    ManageMembers = 4,
    ManageApiKeys = 5
}
