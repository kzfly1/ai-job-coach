using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace AIJobCoach.Api.Common.Http;

public static class CurrentUser
{
    /// <summary>
    /// Reads the authenticated user's id from JWT claims.
    /// </summary>
    public static bool TryGetUserId(this ControllerBase controller, out Guid userId)
    {
        var userIdValue =
            controller.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? controller.User.FindFirstValue(JwtRegisteredClaimNames.Sub);

        return Guid.TryParse(userIdValue, out userId);
    }
}
