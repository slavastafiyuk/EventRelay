using EventRelay.ControlPlaneService.Application.Exceptions;
using EventRelay.ControlPlaneService.Infrastructure.Persistence.Exceptions.Abstraction;
using Microsoft.EntityFrameworkCore;

namespace EventRelay.ControlPlaneService.Infrastructure.Persistence.Exceptions;

public sealed class UserIdentityConflictHandler : IDbUpdateExceptionHandler
{
    public string ConstraintName => "IX_users_Issuer_Subject";

    public Exception Handle(DbUpdateException exception)
        => new DuplicateUserIdentityException(exception);
}
