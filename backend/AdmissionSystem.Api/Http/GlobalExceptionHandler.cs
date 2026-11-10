using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AdmissionSystem.Api.Http;

/// <summary>
/// Turns unhandled exceptions into ProblemDetails JSON with <c>code</c> and <c>message</c>,
/// so clients never see an HTML error page or an empty 500.
/// </summary>
public sealed class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (status, code, message) = Map(exception);

        if (status >= StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception, "Unhandled exception for {Method} {Path}",
                httpContext.Request.Method, httpContext.Request.Path);
        }
        else
        {
            logger.LogInformation("Mapped {ExceptionType} to {Status} {Code}",
                exception.GetType().Name, status, code);
        }

        httpContext.Response.StatusCode = status;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = ApiProblem.Create(status, code, message),
        });
    }

    private static (int Status, string Code, string Message) Map(Exception exception)
    {
        // 23505 = unique_violation: the partial unique index caught a duplicate active hold.
        if (FindPostgresException(exception) is { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return (StatusCodes.Status409Conflict, ErrorCodes.DuplicateHold,
                "This student already has an active or confirmed application for this class.");
        }

        if (exception is BadHttpRequestException badRequest)
        {
            return (badRequest.StatusCode, ErrorCodes.ForStatus(badRequest.StatusCode),
                "The request could not be read. Check that the body is valid JSON.");
        }

        return (StatusCodes.Status500InternalServerError, ErrorCodes.InternalError,
            "An unexpected server error occurred. Please try again.");
    }

    /// <summary>Walks the inner-exception chain (e.g. <see cref="DbUpdateException"/> wraps it).</summary>
    private static PostgresException? FindPostgresException(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is PostgresException postgres)
            {
                return postgres;
            }
        }

        return null;
    }
}
