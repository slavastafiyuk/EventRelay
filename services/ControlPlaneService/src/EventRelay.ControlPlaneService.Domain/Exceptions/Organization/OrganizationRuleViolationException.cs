namespace EventRelay.ControlPlaneService.Domain.Exceptions.Organization;

public sealed class OrganizationRuleViolationException(string message)
    : Exception(message);
