using EventRelay.ControlPlaneService.Api.Controllers.Base;
using EventRelay.ControlPlaneService.Application.Exceptions;
using EventRelay.ControlPlaneService.Presentation.Abstractions;
using EventRelay.ControlPlaneService.Presentation.Contracts.Api;
using Microsoft.AspNetCore.Mvc;

namespace EventRelay.ControlPlaneService.Api.Controllers.Public;

[Route("api/projects/{projectId:guid}/api-keys")]
public sealed class ProjectApiKeysController(
    IProjectApiHandler handler) : PublicControllerBase
{

    [HttpPost]
    [ProducesResponseType(typeof(IssuedApiDTO), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Issue(Guid projectId, [FromBody] CreateApiKeyDTO dto, CancellationToken ct)
    {
        try
        {
            var result = await handler.IssueAsync(projectId, dto, ct);

            Response.Headers.CacheControl = "no-store";

            return StatusCode(201, result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (ProjectAccessDeniedException)
        {
            return Forbid();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Status = 400,
                Title = "Invalid API key request.",
                Detail = ex.Message
            });
        }
    }

    [HttpDelete("{apiKeyId:guid}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Revoke(Guid projectId, Guid apiKeyId, CancellationToken ct)
    {
        try
        {
            await handler.RevokeAsync(projectId, apiKeyId, ct);

            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (ProjectAccessDeniedException)
        {
            return Forbid();
        }
    }
}
