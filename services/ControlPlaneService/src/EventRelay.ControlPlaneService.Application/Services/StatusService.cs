using EventRelay.ControlPlaneService.Application.Abstractions;
using EventRelay.ControlPlaneService.Application.Models;

namespace EventRelay.ControlPlaneService.Application.Services;

public sealed class StatusService : IStatusService
{
    public ProjectStatus GetStatus()
    {
        return new ProjectStatus(
            Service: "ControlPlaneService",
            Status: "ok");
    }
}