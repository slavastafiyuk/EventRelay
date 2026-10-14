using EventRelay.ProjectService.Presentation.Abstractions;
using EventRelay.ProjectService.Presentation.Handlers;
using Microsoft.Extensions.DependencyInjection;

namespace EventRelay.ProjectService.Presentation;

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