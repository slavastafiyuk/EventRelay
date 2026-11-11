using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EventRelay.ControlPlaneService.Api.Controllers.Base;

[ApiController]
[Authorize]
public abstract class PublicControllerBase : ControllerBase
{
}
