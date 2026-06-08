using FluentValidation.Results;

namespace AIJobCoach.Api.Common.Http;

public sealed record ValidationErrorResponse (
    string Code,
    string Message,
    Dictionary<string, string[]> Errors)
{
    public static ValidationErrorResponse From(ValidationResult validationResult)
    {
        var errors = validationResult.Errors
            .GroupBy(error => error.PropertyName)
            .ToDictionary(
                group => group.Key,
                group => group.Select(error => error.ErrorMessage).ToArray());

        return new ValidationErrorResponse(
            "VALIDATION_ERROR",
            "Validation failed.",
            errors);
    }
}