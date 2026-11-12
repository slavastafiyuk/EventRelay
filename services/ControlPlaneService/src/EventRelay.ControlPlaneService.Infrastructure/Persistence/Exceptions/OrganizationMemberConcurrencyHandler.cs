
using EventRelay.ControlPlaneService.Application.Exceptions.Organization;
using EventRelay.ControlPlaneService.Infrastructure.Persistence.Exceptions.Abstraction;
using EventRelay.ControlPlaneService.Infrastructure.Persistence.Models.Organization;
using Microsoft.EntityFrameworkCore;

namespace EventRelay.ControlPlaneService.Infrastructure.Persistence.Exceptions;

public sealed class OrganizationMemberConcurrencyHandler : IDbUpdateConcurrencyHandler
{
    public bool CanHandle(DbUpdateConcurrencyException exception)
    {
        return exception.Entries.Any(
            entry => entry.Entity is OrganizationMemberRecord);
    }

    public Exception Handle(DbUpdateConcurrencyException exception)
    {
        return new OrganizationMembershipConflictException(
            "Organization membership changed during the operation.",
            exception);
    }
}
