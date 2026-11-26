using EventRelay.ControlPlaneService.Application.Exceptions;
using Microsoft.AspNetCore.Diagnostics;

namespace EventRelay.ControlPlaneService.Api.ExceptionHandling;

public sealed class TransactionExceptionHandler
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not TransactionConflictException)
            return false;

        await Results.Problem(
            statusCode: StatusCodes.Status409Conflict,
            title: "Transaction conflict.",
            detail: "The operation conflicted with another " +
                    "transaction. Refresh and try again."
        ).ExecuteAsync(context);

        return true;
    }
}
