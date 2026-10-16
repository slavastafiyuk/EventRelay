using EventRelay.ProjectService.Application.Abstractions;
using EventRelay.ProjectService.Presentation.Abstractions;
using EventRelay.ProjectService.Presentation.Contracts;
using Mapster;

namespace EventRelay.ProjectService.Presentation.Handlers;

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
