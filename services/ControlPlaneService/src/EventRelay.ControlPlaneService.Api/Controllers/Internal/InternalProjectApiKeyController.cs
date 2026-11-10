using EventRelay.ControlPlaneService.Api.Controllers.Base;
using EventRelay.ControlPlaneService.Presentation.Abstractions;
using EventRelay.ControlPlaneService.Presentation.Contracts.Api;
using Microsoft.AspNetCore.Mvc;

namespace EventRelay.ControlPlaneService.Api.Controllers.Internal;

[Route("internal/api-keys")]
public sealed class InternalProjectApiKeyController(
    IProjectApiHandler projectApiHandler) : InternalControllerBase
{

    [HttpPost("resolve")]
    [ProducesResponseType(typeof(ResolvedApiKeyDTO), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ResolvedApiKeyDTO>> Resolve(
        [FromBody] ResolveApiKeyDTO dto,
        CancellationToken cancellationToken)
    {
        var result = await projectApiHandler.ResolveAsync(dto, cancellationToken);

        if (result is null)
            return NotFound();

        Response.Headers.CacheControl = "no-store";

        return Ok(result);
    }
}
