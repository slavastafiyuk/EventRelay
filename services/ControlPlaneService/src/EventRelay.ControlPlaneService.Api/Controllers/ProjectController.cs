using EventRelay.ControlPlaneService.Application.Exceptions;
using EventRelay.ControlPlaneService.Presentation.Abstractions;
using EventRelay.ControlPlaneService.Presentation.Contracts.Project;
using Microsoft.AspNetCore.Mvc;

namespace EventRelay.ControlPlaneService.Api.Controllers;

[ApiController]
[Route("api/projects")]
public sealed class ProjectController(
    IProjectHandler projectHandler) : ControllerBase
{

    [HttpPost]
    [ProducesResponseType(typeof(ProjectKeyDTO), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ProjectKeyDTO>> Create(
        [FromBody] ProjectCreateDTO dto,
        CancellationToken cancellationToken)
    {
        try
        {
            var project = await projectHandler.CreateAsync(
                dto,
                cancellationToken);

            return CreatedAtAction(
                nameof(GetById),
                new { id = project.Id },
                project);
        }
        catch (DuplicateProjectNameException)
        {
            return Conflict(new ProblemDetails
            {
                Title = "Project name already exists.",
                Status = StatusCodes.Status409Conflict
            });
        }
        catch (ArgumentException ex)
        {
            return StatusCode(StatusCodes.Status400BadRequest, new ProblemDetails
            {
                Title = "An error occurred while creating the project.",
                Status = StatusCodes.Status400BadRequest,
                Detail = ex.Message
            });
        }
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ProjectDTO), StatusCodes.Status200OK)]
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
