using PrivateLibrary.Models;

namespace PrivateLibrary.Services;

public class PosterResolverService
{
    private readonly TmdbService _tmdbService;

    public PosterResolverService(TmdbService tmdbService)
    {
        _tmdbService = tmdbService;
    }

    public string? ResolvePosterUrl(Film film)
    {
        if (!string.IsNullOrWhiteSpace(film.Titel) &&
            MovieMappings.ManualImageMapping.TryGetValue(film.Titel, out var manualImage))
        {
            return $"images/manual/{manualImage}";
        }

        if (!string.IsNullOrWhiteSpace(film.PosterPath))
        {
            return _tmdbService.GetPosterUrl(film.PosterPath, 400);
        }

        return "images/placeholder.png";
    }
}
