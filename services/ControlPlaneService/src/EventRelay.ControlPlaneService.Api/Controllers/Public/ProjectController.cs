using EventRelay.ControlPlaneService.Api.Controllers.Base;
using EventRelay.ControlPlaneService.Presentation.Abstractions;
using EventRelay.ControlPlaneService.Presentation.Contracts.Project;
using Microsoft.AspNetCore.Mvc;

namespace EventRelay.ControlPlaneService.Api.Controllers.Public;

[Route("api/projects")]
public sealed class ProjectController(
    IProjectHandler projectHandler) : PublicControllerBase
{

    [HttpPost]
    [ProducesResponseType(typeof(ProjectKeyDTO), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ProjectKeyDTO>> Create(
        [FromBody] ProjectCreateDTO dto,
        CancellationToken cancellationToken)
    {
        var project = await projectHandler.CreateAsync(
            dto,
            cancellationToken);

        return CreatedAtAction(
            nameof(GetById),
            new { id = project.Id },
            project);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ProjectDTO), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProjectDTO>> GetById(
    Guid id,
    CancellationToken cancellationToken)
    {
        var project = await projectHandler.GetByIdAsync(
        id,
        cancellationToken);

        if (project is null)
            return NotFound();

        return Ok(project);
    }
}
