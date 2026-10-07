// Базовий контролер: /api/v1/lab, ProblemDetails, без [Authorize]
using Microsoft.AspNetCore.Mvc;

namespace MedLink.LIS.Api.Controllers;

[ApiController]
[Route("api/v1/lab")]
[Produces("application/json")]
public abstract class LisControllerBase : ControllerBase
{
}
