namespace AdmissionSystem.Api.Domain;

/// <summary>
/// The only valid values for <see cref="Hold.Status"/>. Use these constants everywhere;
/// never scatter the raw strings through the code.
/// </summary>
public static class HoldStatus
{
    public const string Held = "HELD";
    public const string Confirmed = "CONFIRMED";
    public const string Expired = "EXPIRED";
    public const string Cancelled = "CANCELLED";

    public static readonly IReadOnlyList<string> All = [Held, Confirmed, Expired, Cancelled];

    /// <summary>
    /// Status as seen by clients: a <see cref="Held"/> hold whose expiry has passed is
    /// reported as <see cref="Expired"/> even if the row has not been flipped yet (lazy expiry).
    /// </summary>
    public static string Effective(string status, DateTimeOffset expiresAt, DateTimeOffset now) =>
        status == Held && expiresAt <= now ? Expired : status;
}

