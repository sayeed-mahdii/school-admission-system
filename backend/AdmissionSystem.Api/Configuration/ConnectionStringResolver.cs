namespace AdmissionSystem.Api.Configuration;

public static class ConnectionStringResolver
{
    public const string CanonicalName = "DefaultConnection";
    public const string FallbackVariable = "DATABASE_CONNECTION_STRING";

    /// <summary>
    /// Precedence: <c>ConnectionStrings__DefaultConnection</c> (canonical) first, then
    /// <c>DATABASE_CONNECTION_STRING</c>. Both must be Npgsql key=value strings
    /// (<c>Host=...;Database=...;Username=...;Password=...</c>), not <c>postgres://</c> URIs.
    /// </summary>
    public static string Resolve(IConfiguration configuration)
    {
        var canonical = configuration.GetConnectionString(CanonicalName);
        if (!string.IsNullOrWhiteSpace(canonical))
        {
            return canonical;
        }

        var fallback = configuration[FallbackVariable];
        if (!string.IsNullOrWhiteSpace(fallback))
        {
            return fallback;
        }

        throw new InvalidOperationException(
            $"No database connection string configured. Set 'ConnectionStrings__{CanonicalName}' " +
            $"or '{FallbackVariable}' to an Npgsql connection string.");
    }
}

