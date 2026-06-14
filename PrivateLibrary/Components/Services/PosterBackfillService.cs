using Microsoft.EntityFrameworkCore;
using PrivateLibrary.Data;

namespace PrivateLibrary.Services;

/// <summary>
/// Fills in missing poster paths for movies that have a TMDB id but no stored poster.
/// Runs once shortly after startup and writes the results straight to the database, so
/// the movie page never needs to call TMDB while loading.
/// </summary>
public sealed class PosterBackfillService(
    IServiceProvider services,
    ILogger<PosterBackfillService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tmdbService = scope.ServiceProvider.GetRequiredService<TmdbService>();

        List<Movie> missing;
        try
        {
            missing = await db.Movies
                .Where(m => m.TmdbId != null && (m.PosterPath == null || m.PosterPath == ""))
                .ToListAsync(stoppingToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Poster backfill: movies could not be loaded.");
            return;
        }

        if (missing.Count == 0)
        {
            return;
        }

        logger.LogInformation("Poster backfill: fetching {Count} missing posters.", missing.Count);

        var changed = false;
        foreach (var movie in missing)
        {
            if (stoppingToken.IsCancellationRequested) break;

            try
            {
                var path = await tmdbService.FetchPosterPathAsync(movie.TmdbId!.Value, movie.Kategorie);
                if (!string.IsNullOrWhiteSpace(path))
                {
                    movie.PosterPath = path;
                    changed = true;
                }
                await Task.Delay(120, stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Poster backfill: could not fetch poster for {Title}.", movie.Titel);
            }
        }

        if (changed)
        {
            try
            {
                await db.SaveChangesAsync(stoppingToken);
                logger.LogInformation("Poster backfill: database updated.");
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Poster backfill: could not save changes.");
            }
        }
    }
}
