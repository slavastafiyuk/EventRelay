
using EventRelay.ControlPlaneService.Application.Exceptions.Organization;
using EventRelay.ControlPlaneService.Infrastructure.Persistence.Exceptions.Abstraction;
using Microsoft.EntityFrameworkCore;

namespace EventRelay.ControlPlaneService.Infrastructure.Persistence.Exceptions;

public sealed class OrganizationMemberConflictHandler : IDbUpdateExceptionHandler
{
    public string ConstraintName => "PK_organization_members";

    public Exception Handle(DbUpdateException exception)
    {
        return new OrganizationMembershipConflictException(
            "User is already a member of the organization.",
            exception);
    }
}
