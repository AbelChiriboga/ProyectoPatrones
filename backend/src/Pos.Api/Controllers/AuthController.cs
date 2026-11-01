using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pos.Application.Auth;

namespace Pos.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/auth")]
public sealed class AuthController(LoginUseCase login) : ControllerBase
{
    /// <summary>Autentica al operador y devuelve el token que el frontend enviará en cada petición.</summary>
    [HttpPost("login")]
    public ActionResult<LoginResult> Login([FromBody] LoginRequest request) => Ok(login.Execute(request));
}
