using Microsoft.Extensions.DependencyInjection;

namespace EventRelay.ProjectService.Repository;

public static class DependencyInjection
{
    public static IServiceCollection AddRepositories(
        this IServiceCollection services)
    {
        // Repositories serão registados aqui
        // quando implementarmos a persistência.

        return services;
    }
}