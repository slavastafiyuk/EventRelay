using EventRelay.ControlPlaneService.Presentation.Contracts;

namespace EventRelay.ControlPlaneService.Presentation.Abstractions;

public interface IProjectPingHandler
{
    ProjectPingResponse Handle();
}