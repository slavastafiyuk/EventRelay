using EventRelay.ControlPlaneService.Application.Exceptions.Project;
using EventRelay.ControlPlaneService.Infrastructure.Persistence.Exceptions.Abstraction;
using EventRelay.ControlPlaneService.Infrastructure.Persistence.Models.Project.Access;
using Microsoft.EntityFrameworkCore;

namespace EventRelay.ControlPlaneService.Infrastructure.Persistence.Exceptions;

public sealed class ProjectMemberConcurrencyHandler : IDbUpdateConcurrencyHandler
{
    public bool CanHandle(DbUpdateConcurrencyException exception)
    {
        return exception.Entries.Any(entry => entry.Entity is ProjectMemberRecord);
    }

    public Exception Handle(DbUpdateConcurrencyException exception)
    {
        return new ProjectMembershipConflictException("Project membership changed during the operation.", exception);
    }
}
