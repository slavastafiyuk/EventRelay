using EventRelay.ProjectService.Application.Abstractions;
using EventRelay.ProjectService.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace EventRelay.ProjectService.Application;

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