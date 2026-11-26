namespace EventRelay.ControlPlaneService.Application.Exceptions.Organization;

public sealed class OrganizationMemberOwnsProjectsException(string message) : Exception(message);
