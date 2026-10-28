namespace EventRelay.ControlPlaneService.Domain.Entities.Project.Access;

[Flags]
public enum ProjectPermission
{
    None = 0,
    Read = 1,
    Update = 2,
    Delete = 4,
    ManageMembers = 8,
    ManageApiKeys = 16
}
