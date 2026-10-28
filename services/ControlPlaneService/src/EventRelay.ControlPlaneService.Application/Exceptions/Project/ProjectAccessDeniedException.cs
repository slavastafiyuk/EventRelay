namespace EventRelay.ControlPlaneService.Application.Exceptions;

public sealed class ProjectAccessDeniedException : Exception
{
    public ProjectAccessDeniedException()
        : base("You do not have permission to access this project.")
    {
    }
}
