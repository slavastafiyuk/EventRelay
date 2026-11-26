namespace EventRelay.ControlPlaneService.Domain.Exceptions.Project;

public sealed class ProjectMemberConflictException(string message) : InvalidOperationException(message);
