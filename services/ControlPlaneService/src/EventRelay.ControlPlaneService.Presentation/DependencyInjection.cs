using EventRelay.ControlPlaneService.Presentation.Abstractions;
using EventRelay.ControlPlaneService.Presentation.Handlers;
using Mapster;
using MapsterMapper;
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
        services.AddSingleton(TypeAdapterConfig.GlobalSettings);
        services.AddScoped<IMapper, ServiceMapper>();

        return services;
    }
}
