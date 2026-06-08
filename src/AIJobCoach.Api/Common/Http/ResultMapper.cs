using AIJobCoach.SharedKernel;
using Microsoft.AspNetCore.Mvc;

namespace AIJobCoach.Api.Common.Http;

public static class ResultMapper
{
    public static IActionResult ToActionResult<T>(
        this ControllerBase controller,
        Result<T> result,
        Func<T, IActionResult> onSuccess)
    {
        if (result.IsSuccess)
        {
            if (result.Value is null)
            {
                return controller.StatusCode(
                    StatusCodes.Status500InternalServerError,
                    new Error("INVALID_RESULT_STATE", "Successful result does not contain a value"));
            }
            return onSuccess(result.Value);
        }

        var error = result.Error
                    ?? new Error("UNKNOWN_ERROR", "An unknown error occurred.");

        return error.Code switch
        {
            "EMAIL_ALREADY_EXISTS" =>
                controller.Conflict(error),
            
            "INVALID_CREDENTIALS" =>
                controller.StatusCode(StatusCodes.Status401Unauthorized, error),

            "USER_NOT_FOUND" or "RESUME_NOT_FOUND"=>
                controller.NotFound(error),

            "UNSUPPORTED_FILE_TYPE" or "FILE_TOO_LARGE" =>
                controller.UnprocessableEntity(error),
            
            _ =>
                controller.BadRequest(error)
        };
    }
}