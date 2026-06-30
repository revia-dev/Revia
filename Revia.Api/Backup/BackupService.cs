using Microsoft.EntityFrameworkCore;
using Revia.Data;
using Revia.Services;
using Revia.Shared.Backup;

namespace Revia.Api.Backup;

/// <summary>
/// Produces/consumes the cross-app <see cref="BackupDocument"/> for the signed-in
/// user's household. Maps the web's <see cref="Movie"/> entity (German columns,
/// comma-joined lists, '#'-tag series) to/from the shared, English, camelCase JSON the
/// native app reads — so a backup file round-trips the movie catalogue between the two.
/// </summary>
public sealed class BackupService(AppDbContext db, CurrentUserContext currentUser)
{
    private readonly AppDbContext _db = db;
    private readonly CurrentUserContext _currentUser = currentUser;

    public async Task<BackupDocument> ExportAsync(CancellationToken ct = default)
    {
        var householdId = await _currentUser.RequireHouseholdIdAsync();
        var movies = await _db.Movies.AsNoTracking()
            .Where(m => m.HouseholdId == householdId)
            .ToListAsync(ct);

        return new BackupDocument
        {
            Movies = movies.Select(ToBak).ToList(),
        };
    }

    /// <summary>Selective restore: overwrites only the tables present in the document.
    /// Returns the names of the tables that were overwritten.</summary>
    public async Task<List<string>> RestoreAsync(BackupDocument doc, CancellationToken ct = default)
    {
        var householdId = await _currentUser.RequireHouseholdIdAsync();
        var overwritten = new List<string>();

        if (doc.Movies is not null)
        {
            var existing = _db.Movies.Where(m => m.HouseholdId == householdId);
            _db.Movies.RemoveRange(existing);
            foreach (var bak in doc.Movies)
            {
                _db.Movies.Add(FromBak(bak, householdId));
            }
            overwritten.Add("movies");
        }

        await _db.SaveChangesAsync(ct);
        return overwritten;
    }

    // ---- mapping (mirrors the native MovieBak + the CSV transfer rules) ----

    private static MovieBak ToBak(Movie m)
    {
        var (seriesName, tags) = SplitTags(m.Tags);
        return new MovieBak
        {
            Id = m.Id,
            Title = m.Titel ?? string.Empty,
            Year = int.TryParse(m.Jahr, out var y) ? y : null,
            AgeRating = m.Fsk,
            Category = NormalizeCategory(m.Kategorie),
            Providers = SplitList(m.Anbieter),
            Tags = tags,
            Plot = null,                 // not stored on the web Movie entity
            RuntimeMinutes = m.Runtime,
            SeriesName = seriesName,
            Rating = null,               // no rating column on the web Movie entity
            Watched = false,
            PosterPath = m.PosterPath,
        };
    }

    private static Movie FromBak(MovieBak b, int householdId)
    {
        // Reconstruct the web Tags string: the series goes back to a leading '#'-tag.
        var tagParts = new List<string>();
        if (!string.IsNullOrWhiteSpace(b.SeriesName)) tagParts.Add("#" + b.SeriesName);
        tagParts.AddRange(b.Tags);

        return new Movie
        {
            Id = b.Id,
            HouseholdId = householdId,
            Titel = b.Title,
            Jahr = b.Year?.ToString(),
            Fsk = b.AgeRating,
            Kategorie = b.Category,
            Anbieter = b.Providers.Count > 0 ? string.Join(", ", b.Providers) : null,
            Tags = tagParts.Count > 0 ? string.Join(", ", tagParts) : null,
            PosterPath = b.PosterPath,
            Runtime = b.RuntimeMinutes,
        };
    }

    private static string NormalizeCategory(string? kategorie)
    {
        var k = (kategorie ?? string.Empty).ToLowerInvariant();
        if (k.Contains("tv")) return "tv";
        if (k.Contains("serie")) return "series";
        return "movie";
    }

    private static List<string> SplitList(string? commaJoined)
        => (commaJoined ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

    /// <summary>Splits the comma-joined Tags into the first '#'-series tag (without '#')
    /// and the remaining plain tags.</summary>
    private static (string? series, List<string> tags) SplitTags(string? commaJoined)
    {
        string? series = null;
        var tags = new List<string>();
        foreach (var raw in SplitList(commaJoined))
        {
            if (raw.StartsWith('#'))
            {
                series ??= raw.TrimStart('#');
            }
            else
            {
                tags.Add(raw);
            }
        }
        return (series, tags);
    }
}
