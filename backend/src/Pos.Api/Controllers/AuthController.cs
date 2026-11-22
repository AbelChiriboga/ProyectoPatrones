using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pos.Application.Auth;

namespace Pos.Api.Controllers;

/// <summary>
/// Controlador de autenticación de operadores y administradores del POS.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/v1/auth")]
[Route("api/auth")]
public sealed class AuthController(LoginUseCase login) : ControllerBase
{
    /// <summary>
    /// Autentica al operador o administrador mediante credenciales y emite un token Bearer JWT.
    /// </summary>
    /// <param name="request">Credenciales del operador (Username y Password).</param>
    /// <returns>Token JWT con expiración, nombre de usuario y rol asignado.</returns>
    /// <response code="200">Autenticación exitosa. Devuelve el token JWT.</response>
    /// <response code="400">Datos requeridos incompletos o inválidos.</response>
    /// <response code="401">Credenciales incorrectas.</response>
    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public ActionResult<LoginResult> Login([FromBody] LoginRequest request) => Ok(login.Execute(request));
}
