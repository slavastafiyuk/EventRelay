using EventRelay.ControlPlaneService.Presentation.Contracts.Ping;

namespace EventRelay.ControlPlaneService.Presentation.Abstractions;

public interface IPingHandler
{
    PingResponseDTO Handle();
}
