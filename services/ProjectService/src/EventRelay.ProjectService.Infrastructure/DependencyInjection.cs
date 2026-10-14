using Microsoft.Extensions.DependencyInjection;

namespace EventRelay.ProjectService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services)
    {
        // DbContext e PostgreSQL serão configurados
        // quando implementarmos a persistência.

        return services;
    }
}