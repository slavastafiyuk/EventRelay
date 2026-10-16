using EventRelay.ProjectService.Application.Abstractions.Persistence;
using EventRelay.ProjectService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EventRelay.ProjectService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("ProjectDatabase") ??
            throw new InvalidOperationException("ProjectDatabase connection string is missing.");

        services.AddDbContext<ProjectDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();

        return services;
    }
}
