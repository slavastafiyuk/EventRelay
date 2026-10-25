using EventRelay.ControlPlaneService.Application.Abstractions.Persistence;
using EventRelay.ControlPlaneService.Infrastructure.Persistence;
using EventRelay.ControlPlaneService.Infrastructure.Persistence.Exceptions;
using EventRelay.ControlPlaneService.Infrastructure.Persistence.Exceptions.Abstraction;
using EventRelay.ControlPlaneService.Infrastructure.Persistence.Exceptions.Recovery;
using EventRelay.ControlPlaneService.Infrastructure.Persistence.Models.User;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EventRelay.ControlPlaneService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        bool isGeneratingOpenApi = false)
    {
        if (isGeneratingOpenApi)
        {
            // Maintain the DbContext DI registration without
            // requiring a PostgreSQL connection during
            // OpenAPI document generation.
            services.AddDbContext<DbContext>();
        }
        else
        {
            var connectionString =
                configuration.GetConnectionString("ProjectDatabase")
                ?? throw new InvalidOperationException(
                    "ProjectDatabase connection string is missing.");

            services.AddDbContext<ControlPlaneDbContext>(options => options.UseNpgsql(connectionString));
        }

        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        services.AddSingleton<IDbUpdateExceptionHandler, ProjectNameConflictHandler>();
        services.AddSingleton<IDbUpdateExceptionHandler, UserIdentityConflictHandler>();
        services.AddSingleton<DbUpdateExceptionTranslator>();
        services.AddSingleton<IDbUpdateRecoveryStrategy>(new DetachSingleAddedEntryRecovery<UserRecord>("IX_users_Issuer_Subject"));
        services.AddSingleton<DbUpdateExceptionPipeline>();

        return services;
    }
}
