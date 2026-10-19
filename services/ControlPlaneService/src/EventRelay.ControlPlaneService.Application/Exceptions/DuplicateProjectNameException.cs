namespace EventRelay.ControlPlaneService.Application.Exceptions;

public sealed class DuplicateProjectNameException : Exception
{
    public DuplicateProjectNameException(Exception innerException)
        : base(
            "A project with this name already exists.",
            innerException)
    {
    }
}
