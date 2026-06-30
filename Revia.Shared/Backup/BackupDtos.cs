namespace Revia.Shared.Backup;

/// <summary>
/// Portable backup document, the cross-app contract shared with the native app. Each
/// table is optional: a backup only carries the tables it covers, and a restore
/// overwrites only the tables present in the file (selective restore). Field names are
/// serialised camelCase to match the native app's JSON exactly.
///
/// This .NET side currently maps the <see cref="Movies"/> catalogue (clean Guid ids,
/// household-scoped). Other tables in a native backup are accepted and ignored here;
/// they reference user/person ids that differ between the two systems and need separate
/// reconciliation.
/// </summary>
public sealed class BackupDocument
{
    public int FormatVersion { get; set; } = 1;
    public DateTime ExportedAt { get; set; } = DateTime.UtcNow;

    public List<MovieBak>? Movies { get; set; }

    /// <summary>Table keys actually present (non-null) — the ones a restore overwrites.</summary>
    public List<string> PresentTables()
    {
        var t = new List<string>();
        if (Movies is not null) t.Add("movies");
        return t;
    }
}

/// <summary>A movie in the backup. Field names mirror the native <c>MovieBak</c>.</summary>
public sealed class MovieBak
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public int? Year { get; set; }
    public string? AgeRating { get; set; }
    public string Category { get; set; } = "movie";
    public List<string> Providers { get; set; } = new();
    public List<string> Tags { get; set; } = new();
    public string? Plot { get; set; }
    public int? RuntimeMinutes { get; set; }
    public string? SeriesName { get; set; }
    public double? Rating { get; set; }
    public bool Watched { get; set; }
    public string? PosterPath { get; set; }
}
