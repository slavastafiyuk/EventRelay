using EventRelay.ControlPlaneService.Application.Abstractions;
using EventRelay.ControlPlaneService.Presentation.Abstractions;
using EventRelay.ControlPlaneService.Presentation.Contracts;
using Mapster;

namespace EventRelay.ControlPlaneService.Presentation.Handlers;

public sealed class ProjectPingHandler(
    IProjectStatusService projectStatusService)
    : IProjectPingHandler
{
    public ProjectPingResponse Handle()
    {
        var status = projectStatusService.GetStatus();

        return status.Adapt<ProjectPingResponse>();
    }
}
