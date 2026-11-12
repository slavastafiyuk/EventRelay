
using EventRelay.ControlPlaneService.Application.Exceptions.Organization;
using EventRelay.ControlPlaneService.Domain.Exceptions.Organization;
using Microsoft.AspNetCore.Diagnostics;

namespace EventRelay.ControlPlaneService.Api.ExceptionHandling;

public sealed class OrganizationExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext context,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (!context.Request.Path.StartsWithSegments("/api/organizations"))
            return false;

        var isMemberAction = context.Request.Path.Value?.EndsWith(
            "/members/actions", StringComparison.OrdinalIgnoreCase) == true;

        (int Status, string Title)? error = exception switch
        {
            KeyNotFoundException when isMemberAction =>
                (StatusCodes.Status404NotFound, "Resource not found."),

            UnauthorizedAccessException when isMemberAction =>
                (StatusCodes.Status403Forbidden, "Access denied."),

            OrganizationMembershipConflictException when isMemberAction =>
                (StatusCodes.Status409Conflict, "Organization membership conflict."),

            OrganizationRuleViolationException when isMemberAction =>
                (StatusCodes.Status409Conflict, "Organization member operation conflict."),

            ArgumentException when isMemberAction =>
                (StatusCodes.Status400BadRequest, "Invalid organization member request."),

            ArgumentException =>
                (StatusCodes.Status400BadRequest, "Invalid organization request."),

            _ => null
        };

        if (error is null)
            return false;

        await Results.Problem(
            statusCode: error.Value.Status,
            title: error.Value.Title,
            detail: exception.Message
        ).ExecuteAsync(context);

        return true;
    }
}
