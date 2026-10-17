using EventRelay.ControlPlaneService.Application.Models;

namespace EventRelay.ControlPlaneService.Application.Abstractions;

public interface IProjectStatusService
{
    ProjectStatus GetStatus();
}