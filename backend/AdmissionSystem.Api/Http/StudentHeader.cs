using AdmissionSystem.Api.Domain;
using Microsoft.AspNetCore.Mvc;

namespace AdmissionSystem.Api.Http;

public static class StudentHeader
{
    public const string HeaderName = "X-Student-Id";

    public static bool TryGetStudentId(
        HttpRequest request,
        out string studentId,
        out IActionResult? errorResult)
    {
        studentId = string.Empty;
        errorResult = null;

        if (!request.Headers.TryGetValue(HeaderName, out var values))
        {
            errorResult = ApiProblem.BadRequest(
                $"The '{HeaderName}' header is required.",
                ErrorCodes.MissingStudentId);
            return false;
        }

        var trimmed = values.ToString().Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            errorResult = ApiProblem.BadRequest(
                $"The '{HeaderName}' header must not be empty.",
                ErrorCodes.MissingStudentId);
            return false;
        }

        if (trimmed.Length > Hold.StudentIdMaxLength)
        {
            errorResult = ApiProblem.BadRequest(
                $"The '{HeaderName}' header must be at most {Hold.StudentIdMaxLength} characters.");
            return false;
        }

        studentId = trimmed;
        return true;
    }
}
