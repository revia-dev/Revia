using PrivateLibrary.Models;

namespace PrivateLibrary.Services;

public sealed class PosterBackfillService(
    IServiceProvider services,
    ILogger<PosterBackfillService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

        using var scope = services.CreateScope();
        var csvService = scope.ServiceProvider.GetRequiredService<MovieCsvService>();
        var tmdbService = scope.ServiceProvider.GetRequiredService<TmdbService>();

        List<Film> films;
        try
        {
            films = await csvService.LoadMoviesFromFileAsync();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Poster backfill: CSV could not be loaded.");
            return;
        }

        var missing = films
            .Where(f => f.TmdbId.HasValue && string.IsNullOrWhiteSpace(f.PosterPath))
            .ToList();

        if (missing.Count == 0)
            return;

        logger.LogInformation("Poster backfill: fetching {Count} missing posters.", missing.Count);

        var changed = false;
        foreach (var film in missing)
        {
            if (stoppingToken.IsCancellationRequested) break;

            try
            {
                var path = await tmdbService.FetchPosterPathAsync(film.TmdbId!.Value, film.Kategorie);
                if (!string.IsNullOrWhiteSpace(path))
                {
                    var target = films.First(f => f.Id == film.Id);
                    target.PosterPath = path;
                    changed = true;
                }
                await Task.Delay(120, stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Poster backfill: could not fetch poster for {Title}.", film.Titel);
            }
        }

        if (changed)
        {
            try
            {
                await csvService.SaveMoviesToFileAsync(films);
                logger.LogInformation("Poster backfill: CSV updated.");
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Poster backfill: could not save CSV.");
            }
        }
    }
}
