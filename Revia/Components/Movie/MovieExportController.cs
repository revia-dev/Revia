using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Revia.Data;
using Revia.Models;
using Revia.Services;

namespace Revia.Components.Movie;

/// <summary>
/// Streams the current household's movies as a CSV download in the legacy format.
/// The global RequireAuthenticatedUser fallback policy applies.
/// </summary>
[ApiController]
public sealed class MovieExportController : ControllerBase
{
    [HttpGet("/movies/export.csv")]
    public async Task<IResult> Export(
        [FromServices] AppDbContext db,
        [FromServices] MovieCsvService csvService,
        CancellationToken cancellationToken)
    {
        var householdRaw = User.FindFirstValue(HouseholdClaims.HouseholdId);
        if (!int.TryParse(householdRaw, out var householdId))
        {
            return Results.Unauthorized();
        }

        var movies = await db.Movies
            .AsNoTracking()
            .Where(x => x.HouseholdId == householdId)
            .OrderBy(x => x.Titel)
            .ToListAsync(cancellationToken);

        var films = movies.Select(m => new Film
        {
            Id = m.Id,
            TmdbId = m.TmdbId,
            Titel = m.Titel,
            Jahr = m.Jahr,
            Fsk = m.Fsk,
            Kategorie = m.Kategorie,
            Anbieter = Split(m.Anbieter),
            Tags = Split(m.Tags),
            PosterPath = m.PosterPath
        });

        var stream = new MemoryStream();
        await csvService.WriteMoviesAsync(stream, films);
        stream.Position = 0;

        var fileName = $"movies-{DateTime.Now:yyyy-MM-dd}.csv";
        return Results.File(stream, "text/csv", fileName);
    }

    private static List<string> Split(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? new List<string>()
            : value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
}
