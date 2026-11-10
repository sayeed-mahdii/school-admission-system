namespace AdmissionSystem.Api.Domain;

/// <summary>
/// A student's application for a seat in a class. A seat is occupied while the hold is
/// <see cref="HoldStatus.Held"/> and not yet expired, or <see cref="HoldStatus.Confirmed"/>.
/// </summary>
public class Hold
{
    public const int StudentIdMaxLength = 64;
    public const int StatusMaxLength = 16;

    public Guid Id { get; set; }

    public Guid ClassId { get; set; }

    public ClassEntity? Class { get; set; }

    public string StudentId { get; set; } = string.Empty;

    /// <summary>One of the <see cref="HoldStatus"/> constants.</summary>
    public string Status { get; set; } = HoldStatus.Held;

    /// <summary>Always written in UTC (offset 0); Npgsql rejects non-zero offsets for timestamptz.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Always written in UTC (offset 0).</summary>
    public DateTimeOffset ExpiresAt { get; set; }
}

