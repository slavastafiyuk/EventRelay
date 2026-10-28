using EventRelay.ControlPlaneService.Application.Exceptions.Project;
using EventRelay.ControlPlaneService.Infrastructure.Persistence.Exceptions.Abstraction;
using Microsoft.EntityFrameworkCore;

namespace EventRelay.ControlPlaneService.Infrastructure.Persistence.Exceptions;

public sealed class ProjectNameConflictHandler : IDbUpdateExceptionHandler
{
    public string ConstraintName => "IX_projects_Name";

    public Exception Handle(DbUpdateException exception)
        => new DuplicateProjectNameException(exception);
}
