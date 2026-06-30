namespace Revia.Data;

/// <summary>
/// One user's personal viewing progress for a single movie or series. This is
/// per-user, not shared across the household: every member keeps their own record
/// of how far they have watched. For a film the resume point is
/// <see cref="PositionSeconds"/>; for a series it is the last watched
/// <see cref="Season"/>/<see cref="Episode"/>. <see cref="Status"/> is the coarse
/// state shown in lists ("unwatched", "inProgress", "watched").
/// </summary>
public sealed class WatchProgress
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Tenant the record belongs to (kept denormalised for cheap filtering,
    /// like every other household-scoped entity).</summary>
    public int HouseholdId { get; set; }

    /// <summary>The <see cref="ApplicationUser"/> this progress belongs to.</summary>
    public string UserId { get; set; } = string.Empty;

    public Guid MovieId { get; set; }
    public Movie? Movie { get; set; }

    /// <summary>"unwatched" | "inProgress" | "watched". String-keyed so it round-trips
    /// through JSON, the offline cache and CSV without an enum-versioning concern.</summary>
    public string Status { get; set; } = "unwatched";

    /// <summary>Resume point for a film, in seconds from the start. Null for a series,
    /// or when the film was never started.</summary>
    public int? PositionSeconds { get; set; }

    /// <summary>For a series: the last watched season (1-based). Null for films.</summary>
    public int? Season { get; set; }

    /// <summary>For a series: the last watched episode within the season (1-based).</summary>
    public int? Episode { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
