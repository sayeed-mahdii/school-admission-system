namespace AdmissionSystem.Api.Http;

/// <summary>Machine-readable <c>code</c> values returned in error bodies.</summary>
public static class ErrorCodes
{
    public const string ValidationError = "VALIDATION_ERROR";
    public const string MissingStudentId = "MISSING_STUDENT_ID";
    public const string NotFound = "NOT_FOUND";
    public const string Forbidden = "FORBIDDEN";
    public const string DuplicateHold = "DUPLICATE_HOLD";
    public const string NoSeats = "NO_SEATS";
    public const string HoldExpired = "HOLD_EXPIRED";
    public const string HoldNotActive = "HOLD_NOT_ACTIVE";
    public const string Conflict = "CONFLICT";
    public const string InternalError = "INTERNAL_ERROR";

    public static string ForStatus(int statusCode) => statusCode switch
    {
        StatusCodes.Status400BadRequest => ValidationError,
        StatusCodes.Status403Forbidden => Forbidden,
        StatusCodes.Status404NotFound => NotFound,
        StatusCodes.Status409Conflict => Conflict,
        >= 500 => InternalError,
        _ => $"HTTP_{statusCode}",
    };
}

