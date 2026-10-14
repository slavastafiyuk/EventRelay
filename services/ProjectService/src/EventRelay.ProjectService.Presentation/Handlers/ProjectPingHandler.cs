using EventRelay.ProjectService.Application.Abstractions;
using EventRelay.ProjectService.Presentation.Abstractions;
using EventRelay.ProjectService.Presentation.Contracts;

namespace EventRelay.ProjectService.Presentation.Handlers;

public sealed class ProjectPingHandler(
    IProjectStatusService projectStatusService)
    : IProjectPingHandler
{
    public ProjectPingResponse Handle()
    {
        var status = projectStatusService.GetStatus();

        return new ProjectPingResponse(
            Service: status.Service,
            Status: status.Status);
    }
}