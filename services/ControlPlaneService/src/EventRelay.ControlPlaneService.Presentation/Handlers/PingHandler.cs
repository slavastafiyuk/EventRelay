using EventRelay.ControlPlaneService.Application.Abstractions;
using EventRelay.ControlPlaneService.Presentation.Abstractions;
using EventRelay.ControlPlaneService.Presentation.Contracts.Ping;
using Mapster;

namespace EventRelay.ControlPlaneService.Presentation.Handlers;

public sealed class PingHandler(
    IStatusService projectStatusService)
    : IPingHandler
{
    public PingResponseDTO Handle()
    {
        var status = projectStatusService.GetStatus();

        return status.Adapt<PingResponseDTO>();
    }
}
