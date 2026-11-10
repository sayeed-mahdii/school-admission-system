namespace AdmissionSystem.Api.Http;

/// <summary>
/// Route ids are bound as strings and parsed here, so a malformed id such as
/// <c>does-not-exist</c> consistently yields 404 instead of a framework 400.
/// </summary>
public static class RouteId
{
    public static bool TryParse(string? raw, out Guid id) =>
        Guid.TryParse(raw?.Trim(), out id);
}

