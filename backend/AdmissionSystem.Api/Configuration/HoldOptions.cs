using System.Globalization;

namespace AdmissionSystem.Api.Configuration;

public class HoldOptions
{
    public const string EnvironmentVariable = "HOLD_DURATION_SECONDS";
    public const int DefaultDurationSeconds = 600;

    public int DurationSeconds { get; set; } = DefaultDurationSeconds;

    public TimeSpan Duration => TimeSpan.FromSeconds(DurationSeconds);

    /// <summary>
    /// Parses <c>HOLD_DURATION_SECONDS</c>. Missing, non-numeric, zero or negative values
    /// fall back to the default of 600 seconds.
    /// </summary>
    public static int ParseDurationSeconds(string? raw) =>
        int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds) && seconds > 0
            ? seconds
            : DefaultDurationSeconds;
}

