using EventRelay.ProjectService.Presentation.Contracts;

namespace EventRelay.ProjectService.Presentation.Abstractions;

public interface IProjectPingHandler
{
    ProjectPingResponse Handle();
}