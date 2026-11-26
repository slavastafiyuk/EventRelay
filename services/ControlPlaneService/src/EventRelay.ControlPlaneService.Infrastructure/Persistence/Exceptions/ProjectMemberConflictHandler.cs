using EventRelay.ControlPlaneService.Application.Exceptions.Project;
using EventRelay.ControlPlaneService.Infrastructure.Persistence.Exceptions.Abstraction;
using Microsoft.EntityFrameworkCore;

namespace EventRelay.ControlPlaneService.Infrastructure.Persistence.Exceptions;

public sealed class ProjectMemberConflictHandler : IDbUpdateExceptionHandler
{
    public string ConstraintName => "PK_project_members";

    public Exception Handle(DbUpdateException exception)
    {
        return new ProjectMembershipConflictException("User is already a member of the project.", exception);
    }
}
