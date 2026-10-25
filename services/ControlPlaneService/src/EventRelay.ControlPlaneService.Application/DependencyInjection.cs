using EventRelay.ControlPlaneService.Application.Abstractions;
using EventRelay.ControlPlaneService.Application.Abstractions.Authentication;
using EventRelay.ControlPlaneService.Application.Abstractions.Projects;
using EventRelay.ControlPlaneService.Application.Abstractions.Users;
using EventRelay.ControlPlaneService.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace EventRelay.ControlPlaneService.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(
        this IServiceCollection services)
    {
        services.AddScoped<IStatusService, StatusService>();
        services.AddScoped<IProjectService, ProjectService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<CurrentUser>();
        services.AddScoped<ICurrentUser>(sp => sp.GetRequiredService<CurrentUser>());
        services.AddScoped<ICurrentUserInitializer>(sp => sp.GetRequiredService<CurrentUser>());

        return services;
    }
}
