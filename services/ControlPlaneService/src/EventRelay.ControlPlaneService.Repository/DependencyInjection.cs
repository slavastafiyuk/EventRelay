using EventRelay.ControlPlaneService.Application.Abstractions;
using EventRelay.ControlPlaneService.Repository.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace EventRelay.ControlPlaneService.Repository;

public static class DependencyInjection
{
    public static IServiceCollection AddRepositories(
        this IServiceCollection services)
    {
        services.AddScoped<IProjectRepository, ProjectRepository>();

        return services;
    }
}
