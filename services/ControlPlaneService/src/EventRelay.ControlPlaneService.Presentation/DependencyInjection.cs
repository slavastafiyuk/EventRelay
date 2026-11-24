using EventRelay.ControlPlaneService.Presentation.Abstractions;
using EventRelay.ControlPlaneService.Presentation.Handlers;
using EventRelay.ControlPlaneService.Presentation.Helpers;
using Microsoft.Extensions.DependencyInjection;

namespace EventRelay.ControlPlaneService.Presentation;

public static class DependencyInjection
{
    public static IServiceCollection AddPresentation(
        this IServiceCollection services)
    {
        services.AddScoped<IPingHandler, PingHandler>();
        services.AddScoped<IProjectHandler, ProjectHandler>();
        services.AddScoped<IUserHandler, UserHandler>();
        services.AddScoped<IProjectApiHandler, ProjectApiHandler>();
        services.AddScoped<IOrganizationHandler, OrganizationHandler>();
        services.AddScoped(typeof(KeyResolver<,>));

        return services;
    }
}
