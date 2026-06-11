using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using AIJobCoach.Api.Common.Http;
using AIJobCoach.Api.Modules.Auth.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AIJobCoach.Api.Modules.Auth.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(
    AuthService authService,
    RegisterRequestValidator registerValidator) : ControllerBase
{
    [HttpPost("register")]
    public async Task<IActionResult> Register(
        RegisterRequest request,
        CancellationToken ct)
    {
        var validationResult = await registerValidator.ValidateAsync(request, ct);

        if (!validationResult.IsValid)
        {
            return UnprocessableEntity(ValidationErrorResponse.From(validationResult));
        }

        var result = await authService.RegisterAsync(request, ct);

        if (!result.IsSuccess)
        {
            return this.ToErrorActionResult(result.Error);
        }

        return Created("/api/auth/me", result.Value);
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(
        LoginRequest request,
        CancellationToken ct)
    {
        var result = await authService.LoginAsync(request, ct);

        if (!result.IsSuccess)
        {
            return this.ToErrorActionResult(result.Error);
        }

        return Ok(result.Value);
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var result = await authService.GetProfileAsync(userId, ct);

        if (!result.IsSuccess)
        {
            return this.ToErrorActionResult(result.Error);
        }

        return Ok(result.Value);
    }

    [Authorize]
    [HttpPut("me")]
    public async Task<IActionResult> UpdateMe(
        UpdateProfileRequest request,
        CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var result = await authService.UpdateProfileAsync(userId, request, ct);

        if (!result.IsSuccess)
        {
            return this.ToErrorActionResult(result.Error);
        }

        return Ok(result.Value);
    }

    private bool TryGetUserId(out Guid userId)
    {
        var userIdValue =
            User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);

        return Guid.TryParse(userIdValue, out userId);
    }
}