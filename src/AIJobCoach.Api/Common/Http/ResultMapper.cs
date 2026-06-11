using AIJobCoach.SharedKernel;
using Microsoft.AspNetCore.Mvc;

namespace AIJobCoach.Api.Common.Http;

public static class ResultMapper
{
    public static IActionResult ToErrorActionResult(
        this ControllerBase controller,
        Error? error)
    {
        error ??= new Error("UNKNOWN_ERROR", "An unknown error occurred.");

        return error.Code switch
        {
            "EMAIL_ALREADY_EXISTS" =>
                controller.Conflict(error),

            "INVALID_CREDENTIALS" =>
                controller.StatusCode(StatusCodes.Status401Unauthorized, error),

            "USER_NOT_FOUND" or "RESUME_NOT_FOUND" =>
                controller.NotFound(error),

            "UNSUPPORTED_FILE_TYPE" or "FILE_TOO_LARGE" =>
                controller.UnprocessableEntity(error),

            _ =>
                controller.BadRequest(error)
        };
    }
}