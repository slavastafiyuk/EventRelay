
using EventRelay.ControlPlaneService.Api.Controllers.Base;
using EventRelay.ControlPlaneService.Presentation.Abstractions;
using EventRelay.ControlPlaneService.Presentation.Contracts.Project.Access;
using EventRelay.ControlPlaneService.Presentation.Contracts.Project.Access.Actions;
using Microsoft.AspNetCore.Mvc;

namespace EventRelay.ControlPlaneService.Api.Controllers.Public;

[Route("api/projects/{id:guid}")]
public sealed class ProjectAccessController(IProjectHandler projectHandler) : PublicControllerBase
{
    [HttpPost("members/actions")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ManageMember(Guid id, [FromBody] ProjectMemberActionDTO dto, CancellationToken cancellationToken)
    {
        await projectHandler.ManageMemberAsync(id, dto, cancellationToken);

        return NoContent();
    }

    [HttpPut("visibility")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ChangeVisibility(Guid id, [FromBody] ProjectVisibilityDTO visibility, CancellationToken cancellationToken)
    {
        await projectHandler.ChangeVisibilityAsync(id, visibility, cancellationToken);

        return NoContent();
    }
}
