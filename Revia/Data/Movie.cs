namespace Revia.Data;

/// <summary>
/// A movie record stored in the database (replaces the former movies.csv source).
/// Multi-valued fields (providers, tags) are kept as comma-joined strings to mirror
/// the CSV format and keep export round-tripping trivial. The view layer continues to
/// use the <c>Film</c> model; <c>MovieService</c> maps between the two.
/// </summary>
public sealed class Movie
{
    /// <summary>Stable id, preserved from the original CSV so exports round-trip.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    public int HouseholdId { get; set; }

    public int? TmdbId { get; set; }

    public string? Titel { get; set; }
    public string? Jahr { get; set; }
    public string? Fsk { get; set; }
    public string? Kategorie { get; set; }

    /// <summary>Comma-joined provider names (e.g. "Blu-ray, Netflix").</summary>
    public string? Anbieter { get; set; }

    /// <summary>Comma-joined tags.</summary>
    public string? Tags { get; set; }

    public string? PosterPath { get; set; }

    /// <summary>Runtime in minutes, fetched from TMDB.</summary>
    public int? Runtime { get; set; }
}
