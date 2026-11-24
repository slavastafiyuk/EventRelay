
using EventRelay.ControlPlaneService.Application.Exceptions;
using EventRelay.ControlPlaneService.Application.Exceptions.Project;
using EventRelay.ControlPlaneService.Domain.Exceptions.Project;
using Microsoft.AspNetCore.Diagnostics;

namespace EventRelay.ControlPlaneService.Api.ExceptionHandling;

public sealed class ProjectExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (!httpContext.Request.Path.StartsWithSegments("/api/projects"))
            return false;

        (int Status, string Title)? error = exception switch
        {
            DuplicateProjectNameException => (StatusCodes.Status409Conflict, "Project name already exists."),

            ProjectAccessDeniedException => (StatusCodes.Status403Forbidden, "Access denied."),

            UnauthorizedAccessException => (StatusCodes.Status403Forbidden, "Access denied."),

            KeyNotFoundException => (StatusCodes.Status404NotFound, "Resource not found."),

            ArgumentException => (StatusCodes.Status400BadRequest, "Invalid project request."),

            ProjectMemberConflictException or ProjectMembershipConflictException => (StatusCodes.Status409Conflict, "Project membership conflict."),
            _ => null
        };

        if (error is null)
            return false;

        await Results.Problem(
            statusCode: error.Value.Status,
            title: error.Value.Title
            ).ExecuteAsync(httpContext);

        return true;
    }
}
