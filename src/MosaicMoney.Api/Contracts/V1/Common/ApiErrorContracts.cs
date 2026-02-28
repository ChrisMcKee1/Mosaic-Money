using System.ComponentModel.DataAnnotations;

namespace MosaicMoney.Api.Contracts.V1;

public sealed record ApiErrorResponse(ApiErrorEnvelope Error);

public sealed record ApiErrorEnvelope(
    string Code,
    string Message,
    string TraceId,
    IReadOnlyList<ApiValidationError>? Details = null);

public sealed record ApiValidationError(string Field, string Message);

public static class ApiValidation
{
    public static IReadOnlyList<ApiValidationError> ValidateDataAnnotations<T>(T model)
    {
        var validationResults = new List<ValidationResult>();
        Validator.TryValidateObject(model!, new ValidationContext(model!), validationResults, validateAllProperties: true);

        return validationResults
            .SelectMany(result =>
            {
                var members = result.MemberNames.Any() ? result.MemberNames : [string.Empty];
                return members.Select(member => new ApiValidationError(member, result.ErrorMessage ?? "Invalid value."));
            })
            .ToList();
    }

    public static IResult ToValidationResult(HttpContext httpContext, IEnumerable<ApiValidationError> errors)
    {
        var details = errors.ToList();
        return Results.BadRequest(new ApiErrorResponse(
            new ApiErrorEnvelope(
                "validation_failed",
                "One or more validation errors occurred.",
                httpContext.TraceIdentifier,
                details)));
    }

    public static IResult ToNotFoundResult(HttpContext httpContext, string code, string message)
    {
        return Results.NotFound(new ApiErrorResponse(
            new ApiErrorEnvelope(code, message, httpContext.TraceIdentifier)));
    }

    public static IResult ToConflictResult(HttpContext httpContext, string code, string message)
    {
        return Results.Conflict(new ApiErrorResponse(
            new ApiErrorEnvelope(code, message, httpContext.TraceIdentifier)));
    }

    public static IResult ToUnauthorizedResult(HttpContext httpContext, string code, string message)
    {
        return Results.Json(
            new ApiErrorResponse(new ApiErrorEnvelope(code, message, httpContext.TraceIdentifier)),
            statusCode: StatusCodes.Status401Unauthorized);
    }

    public static IResult ToForbiddenResult(HttpContext httpContext, string code, string message)
    {
        return Results.Json(
            new ApiErrorResponse(new ApiErrorEnvelope(code, message, httpContext.TraceIdentifier)),
            statusCode: StatusCodes.Status403Forbidden);
    }

    public static IResult ToServiceUnavailableResult(HttpContext httpContext, string code, string message)
    {
        return Results.Json(
            new ApiErrorResponse(new ApiErrorEnvelope(code, message, httpContext.TraceIdentifier)),
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }
}