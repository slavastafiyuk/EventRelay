using EventRelay.ControlPlaneService.Application.Abstractions;
using EventRelay.ControlPlaneService.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace EventRelay.ControlPlaneService.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(
        this IServiceCollection services)
    {
        services.AddScoped<
            IProjectStatusService,
            ProjectStatusService>();

        return services;
    }
}