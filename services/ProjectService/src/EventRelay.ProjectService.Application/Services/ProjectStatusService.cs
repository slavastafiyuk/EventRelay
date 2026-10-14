using EventRelay.ProjectService.Application.Abstractions;
using EventRelay.ProjectService.Application.Models;

namespace EventRelay.ProjectService.Application.Services;

public sealed class ProjectStatusService : IProjectStatusService
{
    public ProjectStatus GetStatus()
    {
        return new ProjectStatus(
            Service: "ProjectService",
            Status: "ok");
    }
}