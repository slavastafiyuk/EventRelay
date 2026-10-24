using EventRelay.ControlPlaneService.Presentation.Abstractions;
using EventRelay.ControlPlaneService.Presentation.Contracts.User;
using Microsoft.AspNetCore.Authorization;

namespace EventRelay.ControlPlaneService.Api.Middlware;

public sealed class EnsureUserProvisionedMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(
        HttpContext context,
        IUserHandler userHandler,
        IConfiguration configuration)
    {
        var endpoint = context.GetEndpoint();

        if (endpoint is null || endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null)
        {
            await next(context);
            return;
        }

        var requiresAuthorization =
            endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Count > 0 ||
            endpoint.Metadata.GetOrderedMetadata<AuthorizationPolicy>().Count > 0;

        if (!requiresAuthorization)
        {
            await next(context);
            return;
        }

        if (context.User.Identity?.IsAuthenticated != true)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        var subject = context.User.FindFirst("sub")?.Value;
        if (string.IsNullOrWhiteSpace(subject))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        var issuer = configuration["Authentication:Issuer"] ??
            throw new InvalidOperationException("Authentication issuer is missing.");

        var name = context.User.FindFirst("name")?.Value;

        if (string.IsNullOrWhiteSpace(name))
            name = context.User.FindFirst("preferred_username")?.Value;

        if (string.IsNullOrWhiteSpace(name))
            name = subject;

        name = name.Trim();

        if (name.Length > 150) name = name[..150];

        var dto = new UserCreateDTO
        {
            Issuer = issuer,
            Subject = subject,
            Name = name
        };

        await userHandler.EnsureExistsAsync(dto, context.RequestAborted);
        await next(context);
    }
}
