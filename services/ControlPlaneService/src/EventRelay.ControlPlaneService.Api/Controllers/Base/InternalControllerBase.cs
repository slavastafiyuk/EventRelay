using EventRelay.ControlPlaneService.Api.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EventRelay.ControlPlaneService.Api.Controllers.Base;

[ApiController]
[ApiExplorerSettings(IgnoreApi = true)]
[Authorize(AuthenticationSchemes = InternalServiceAuthenticationDefaults.Scheme)]
public abstract class InternalControllerBase : ControllerBase
{
}
