using Microsoft.EntityFrameworkCore;
using PrivateLibrary.Data;
using PrivateLibrary.Models;

namespace PrivateLibrary.Services;

/// <summary>
/// Database-backed movie access, scoped to the current household. Returns the
/// <see cref="Film"/> view model so the existing pages, filters and poster
/// resolver keep working unchanged.
/// </summary>
public sealed class MovieService(AppDbContext db, CurrentUserContext currentUser)
{
    private readonly AppDbContext _db = db;
    private readonly CurrentUserContext _currentUser = currentUser;

    public async Task<List<Film>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var householdId = await _currentUser.RequireHouseholdIdAsync();
        var movies = await _db.Movies
            .AsNoTracking()
            .Where(x => x.HouseholdId == householdId)
            .OrderBy(x => x.Titel)
            .ToListAsync(cancellationToken);

        return movies.Select(ToFilm).ToList();
    }

    public async Task<Film?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var householdId = await _currentUser.RequireHouseholdIdAsync();
        var movie = await _db.Movies
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && x.HouseholdId == householdId, cancellationToken);
        return movie is null ? null : ToFilm(movie);
    }

    /// <summary>Creates or updates a movie for the current household.</summary>
    public async Task<Film> UpsertAsync(Film film, CancellationToken cancellationToken = default)
    {
        var householdId = await _currentUser.RequireHouseholdIdAsync();

        var existing = film.Id == Guid.Empty
            ? null
            : await _db.Movies.FirstOrDefaultAsync(x => x.Id == film.Id && x.HouseholdId == householdId, cancellationToken);

        if (existing is null)
        {
            existing = new Movie
            {
                Id = film.Id == Guid.Empty ? Guid.NewGuid() : film.Id,
                HouseholdId = householdId
            };
            _db.Movies.Add(existing);
        }

        ApplyTo(existing, film);
        await _db.SaveChangesAsync(cancellationToken);
        return ToFilm(existing);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var householdId = await _currentUser.RequireHouseholdIdAsync();
        var movie = await _db.Movies.FirstOrDefaultAsync(x => x.Id == id && x.HouseholdId == householdId, cancellationToken);
        if (movie is null)
        {
            return false;
        }

        _db.Movies.Remove(movie);
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static Film ToFilm(Movie m) => new()
    {
        Id = m.Id,
        TmdbId = m.TmdbId,
        Titel = m.Titel,
        Jahr = m.Jahr,
        Fsk = m.Fsk,
        Kategorie = m.Kategorie,
        Anbieter = SplitList(m.Anbieter),
        Tags = SplitList(m.Tags),
        PosterPath = m.PosterPath,
        Runtime = m.Runtime
    };

    private static void ApplyTo(Movie target, Film film)
    {
        target.TmdbId = film.TmdbId;
        target.Titel = film.Titel;
        target.Jahr = film.Jahr;
        target.Fsk = film.Fsk;
        target.Kategorie = film.Kategorie;
        target.Anbieter = JoinList(film.Anbieter);
        target.Tags = JoinList(film.Tags);
        target.PosterPath = film.PosterPath;
        target.Runtime = film.Runtime;
    }

    private static List<string> SplitList(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? new List<string>()
            : value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

    private static string? JoinList(IEnumerable<string>? values)
    {
        if (values is null) return null;
        var joined = string.Join(", ", values.Where(x => !string.IsNullOrWhiteSpace(x)));
        return string.IsNullOrWhiteSpace(joined) ? null : joined;
    }
}
