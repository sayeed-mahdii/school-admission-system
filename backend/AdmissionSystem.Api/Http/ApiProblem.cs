using Microsoft.AspNetCore.Mvc;

namespace AdmissionSystem.Api.Http;

/// <summary>
/// Builds RFC 7807 <see cref="ProblemDetails"/> responses that always carry a
/// machine-readable <c>code</c> and a human-readable <c>message</c>.
/// </summary>
public static class ApiProblem
{
    public const string ContentType = "application/problem+json";

    public static ProblemDetails Create(int status, string code, string message, string? title = null)
    {
        var problem = new ProblemDetails
        {
            Status = status,
            Title = title ?? ReasonPhrase(status),
            Detail = message,
        };
        problem.Extensions["code"] = code;
        problem.Extensions["message"] = message;
        return problem;
    }

    public static ObjectResult Result(int status, string code, string message) =>
        new(Create(status, code, message))
        {
            StatusCode = status,
            ContentTypes = { ContentType },
        };

    public static ObjectResult BadRequest(string message, string code = ErrorCodes.ValidationError) =>
        Result(StatusCodes.Status400BadRequest, code, message);

    public static ObjectResult NotFound(string message) =>
        Result(StatusCodes.Status404NotFound, ErrorCodes.NotFound, message);

    public static ObjectResult Forbidden(string message) =>
        Result(StatusCodes.Status403Forbidden, ErrorCodes.Forbidden, message);

    public static ObjectResult Conflict(string code, string message) =>
        Result(StatusCodes.Status409Conflict, code, message);

    private static string ReasonPhrase(int status) =>
        Microsoft.AspNetCore.WebUtilities.ReasonPhrases.GetReasonPhrase(status) is { Length: > 0 } phrase
            ? phrase
            : "Error";
}

