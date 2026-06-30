using System.ComponentModel.DataAnnotations;

namespace Revia.Shared.Progress;

/// <summary>Coarse personal watch state. String-keyed (not an enum) to round-trip
/// cleanly through JSON, the offline cache and CSV.</summary>
public static class WatchStatus
{
    public const string Unwatched = "unwatched";
    public const string InProgress = "inProgress";
    public const string Watched = "watched";

    public static bool IsValid(string? value)
        => value is Unwatched or InProgress or Watched;

    /// <summary>Normalises any input to a valid status, defaulting to Unwatched.</summary>
    public static string Normalize(string? value)
        => IsValid(value) ? value! : Unwatched;
}

/// <summary>A user's progress for one movie, as returned to the client.</summary>
public sealed class WatchProgressDto
{
    public Guid MovieId { get; set; }
    public string Status { get; set; } = WatchStatus.Unwatched;
    public int? PositionSeconds { get; set; }
    public int? Season { get; set; }
    public int? Episode { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>Upsert payload for the current user's progress on one movie.</summary>
public sealed class SaveWatchProgressRequest
{
    [Required]
    public Guid MovieId { get; set; }

    public string Status { get; set; } = WatchStatus.Unwatched;

    [Range(0, 60 * 60 * 24)]
    public int? PositionSeconds { get; set; }

    [Range(1, 1000)]
    public int? Season { get; set; }

    [Range(1, 100000)]
    public int? Episode { get; set; }
}
