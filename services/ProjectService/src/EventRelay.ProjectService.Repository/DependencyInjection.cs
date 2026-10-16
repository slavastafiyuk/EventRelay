using EventRelay.ProjectService.Application.Abstractions;
using EventRelay.ProjectService.Repository.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace EventRelay.ProjectService.Repository;

public static class DependencyInjection
{
    public static IServiceCollection AddRepositories(
        this IServiceCollection services)
    {
        services.AddScoped<IProjectRepository, ProjectRepository>();

        return services;
    }
}
