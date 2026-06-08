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

        return this.ToActionResult(
            result,
            value => Created("/api/auth/me", value));
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(
        LoginRequest request,
        CancellationToken ct)
    {
        var result = await authService.LoginAsync(request, ct);

        return this.ToActionResult(
            result,
            value => Ok(value));
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

        return this.ToActionResult(
            result,
            value => Ok(value));
    }

    [Authorize]
    [HttpPut("me")]
    public async Task<IActionResult> UpdateMe(
        UpdateProfileRequest request,
        CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();
        
        var result = await authService.UpdateProfileAsync(userId, request, ct);

        return this.ToActionResult(
            result,
            value => Ok(value));
    }

    private bool TryGetUserId(out Guid userId)
    {
        var sub = User.FindFirstValue("sub")
            ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        
        return Guid.TryParse(sub, out userId);
    }
}