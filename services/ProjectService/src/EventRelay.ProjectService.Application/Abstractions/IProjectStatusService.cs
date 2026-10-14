using EventRelay.ProjectService.Application.Models;

namespace EventRelay.ProjectService.Application.Abstractions;

public interface IProjectStatusService
{
    ProjectStatus GetStatus();
}