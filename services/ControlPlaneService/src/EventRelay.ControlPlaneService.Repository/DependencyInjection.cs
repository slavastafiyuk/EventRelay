using EventRelay.ControlPlaneService.Application.Abstractions.Projects;
using EventRelay.ControlPlaneService.Application.Abstractions.Users;
using EventRelay.ControlPlaneService.Repository.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace EventRelay.ControlPlaneService.Repository;

public static class DependencyInjection
{
    public static IServiceCollection AddRepositories(
        this IServiceCollection services)
    {
        services.AddScoped<IProjectRepository, ProjectRepository>();
        services.AddScoped<IUserRepository, UserRepository>();

        return services;
    }
}
