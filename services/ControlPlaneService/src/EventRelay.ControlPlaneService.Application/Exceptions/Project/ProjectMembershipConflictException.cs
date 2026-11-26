namespace EventRelay.ControlPlaneService.Application.Exceptions.Project;

public sealed class ProjectMembershipConflictException(string message, Exception innerException) : Exception(message, innerException);
