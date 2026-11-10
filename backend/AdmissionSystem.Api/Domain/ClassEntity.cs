namespace AdmissionSystem.Api.Domain;

/// <summary>
/// One admission "class". Each shift is its own class with its own seat count
/// (for example "Class 6 - Morning Shift" and "Class 6 - Day Shift").
/// Seat counts are never stored; they are derived from <see cref="Hold"/> rows.
/// </summary>
public class ClassEntity
{
    public const int NameMaxLength = 200;

    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public int TotalSeats { get; set; }

    /// <summary>Always written in UTC (offset 0).</summary>
    public DateTimeOffset CreatedAt { get; set; }

    public List<Hold> Holds { get; set; } = [];
}

