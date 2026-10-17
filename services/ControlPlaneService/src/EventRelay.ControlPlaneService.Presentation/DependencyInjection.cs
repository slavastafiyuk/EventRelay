using EventRelay.ControlPlaneService.Presentation.Abstractions;
using EventRelay.ControlPlaneService.Presentation.Handlers;
using Microsoft.Extensions.DependencyInjection;

namespace EventRelay.ControlPlaneService.Presentation;

public static class DependencyInjection
{
    public static IServiceCollection AddPresentation(
        this IServiceCollection services)
    {
        services.AddScoped<
            IProjectPingHandler,
            ProjectPingHandler>();

        return services;
    }
}