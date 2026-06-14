using Microsoft.EntityFrameworkCore;
using PrivateLibrary.Services;

namespace PrivateLibrary.Data;

/// <summary>
/// One-time import of the legacy movies.csv into the Movies table. Runs only when
/// the table is empty, assigning every imported movie to the default household.
/// </summary>
public static class MovieSeeder
{
    public static async Task ImportCsvIfEmptyAsync(
        AppDbContext db,
        MovieCsvService csvService,
        ILogger logger)
    {
        if (await db.Movies.AnyAsync())
        {
            return;
        }

        var household = await db.Households.OrderBy(x => x.Id).FirstOrDefaultAsync();
        if (household is null)
        {
            return;
        }

        List<Models.Film> films;
        try
        {
            films = await csvService.LoadMoviesFromFileAsync();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Movie import: movies.csv could not be loaded; skipping.");
            return;
        }

        if (films.Count == 0)
        {
            return;
        }

        foreach (var film in films)
        {
            db.Movies.Add(new Movie
            {
                Id = film.Id == Guid.Empty ? Guid.NewGuid() : film.Id,
                HouseholdId = household.Id,
                TmdbId = film.TmdbId,
                Titel = film.Titel,
                Jahr = film.Jahr,
                Fsk = film.Fsk,
                Kategorie = film.Kategorie,
                Anbieter = JoinList(film.Anbieter),
                Tags = JoinList(film.Tags),
                PosterPath = film.PosterPath
            });
        }

        await db.SaveChangesAsync();
        logger.LogInformation("Movie import: imported {Count} movies into household {HouseholdId}.", films.Count, household.Id);
    }

    private static string? JoinList(IEnumerable<string>? values)
    {
        if (values is null) return null;
        var joined = string.Join(", ", values.Where(x => !string.IsNullOrWhiteSpace(x)));
        return string.IsNullOrWhiteSpace(joined) ? null : joined;
    }
}
