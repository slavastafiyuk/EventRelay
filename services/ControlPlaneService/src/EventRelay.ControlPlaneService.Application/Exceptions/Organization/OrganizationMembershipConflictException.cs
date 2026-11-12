namespace EventRelay.ControlPlaneService.Application.Exceptions.Organization;

public sealed class OrganizationMembershipConflictException(
    string message,
    Exception innerException)
    : Exception(message, innerException);
