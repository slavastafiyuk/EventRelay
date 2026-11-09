using EventRelay.ControlPlaneService.Presentation.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EventRelay.ControlPlaneService.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/projects/{projectId:guid}/api-keys")]
public class ProjectApiKeysController(
    IProjectApiHandler projectApiHandler) : ControllerBase
{



}
