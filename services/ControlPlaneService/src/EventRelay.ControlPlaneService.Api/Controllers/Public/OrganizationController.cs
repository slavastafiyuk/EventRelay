
using EventRelay.ControlPlaneService.Api.Controllers.Base;
using EventRelay.ControlPlaneService.Presentation.Abstractions;
using EventRelay.ControlPlaneService.Presentation.Contracts.Organization;
using EventRelay.ControlPlaneService.Presentation.Contracts.Organization.Access.Actions;
using Microsoft.AspNetCore.Mvc;

namespace EventRelay.ControlPlaneService.Api.Controllers.Public;

[Route("api/organizations")]
public sealed class OrganizationController(
    IOrganizationHandler organizationHandler) : PublicControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(OrganizationKeyDTO), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<OrganizationKeyDTO>> Create(
        [FromBody] OrganizationCreateDTO dto,
        CancellationToken cancellationToken)
    {
        try
        {
            var organization = await organizationHandler.CreateAsync(
                dto,
                cancellationToken);

            return CreatedAtAction(
                nameof(GetById),
                new { id = organization.Id },
                organization);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid organization request.",
                Status = StatusCodes.Status400BadRequest,
                Detail = ex.Message
            });
        }
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(OrganizationDTO), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrganizationDTO>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var organization = await organizationHandler.GetByIdAsync(
            id,
            cancellationToken);

        if (organization is null)
            return NotFound();

        return Ok(organization);
    }


    [HttpPost("{id:guid}/members/actions")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ManageMember(
        Guid id,
        [FromBody] OrganizationMemberActionDTO dto,
        CancellationToken cancellationToken)
    {
        try
        {
            await organizationHandler.ManageMemberAsync(
                id,
                dto,
                cancellationToken);

            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid organization member request.",
                Status = StatusCodes.Status400BadRequest,
                Detail = ex.Message
            });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new ProblemDetails
            {
                Title = "Organization member operation conflict.",
                Status = StatusCodes.Status409Conflict,
                Detail = ex.Message
            });
        }
    }
}
