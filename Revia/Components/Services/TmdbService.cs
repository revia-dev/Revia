using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace Revia.Services;

public class TmdbService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    public TmdbService(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _configuration = configuration;
    }

    public async Task<string?> FetchPosterPathAsync(int tmdbId, string? kategorie)
        => (await FetchDetailsAsync(tmdbId, kategorie))?.PosterPath;

    /// <summary>Fetches poster path and runtime (minutes) for a TMDB id.</summary>
    public async Task<TmdbDetails?> FetchDetailsAsync(int tmdbId, string? kategorie)
    {
        var apiKey = _configuration["Tmdb:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
            return null;

        var typePath = string.Equals(kategorie, "tv", StringComparison.OrdinalIgnoreCase)
            ? "tv"
            : "movie";

        var url = $"https://api.themoviedb.org/3/{typePath}/{tmdbId}?api_key={apiKey}&language=de";

        try
        {
            var response = await _httpClient.GetFromJsonAsync<TmdbDetailResponse>(url);
            if (response is null) return null;
            // TV shows expose episode_run_time as an array; take the first entry.
            var runtime = response.Runtime ?? response.EpisodeRunTime?.FirstOrDefault();
            return new TmdbDetails(response.PosterPath, runtime > 0 ? runtime : null);
        }
        catch
        {
            return null;
        }
    }

    public sealed record TmdbDetails(string? PosterPath, int? Runtime);

    public async Task<List<TmdbSearchResult>> SearchMoviesAsync(string query)
    {
        var apiKey = _configuration["Tmdb:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(query))
            return new List<TmdbSearchResult>();

        var url = $"https://api.themoviedb.org/3/search/movie?api_key={apiKey}&language=de-DE&query={Uri.EscapeDataString(query)}";

        try
        {
            var response = await _httpClient.GetFromJsonAsync<TmdbSearchResponse>(url);
            return response?.Results?
                .Where(x => x.Id > 0)
                .Take(10)
                .Select(x => new TmdbSearchResult
                {
                    Id = x.Id,
                    Title = x.Title ?? "Unbekannt",
                    ReleaseYear = string.IsNullOrWhiteSpace(x.ReleaseDate) ? "-" : x.ReleaseDate[..Math.Min(4, x.ReleaseDate.Length)],
                    PosterUrl = string.IsNullOrWhiteSpace(x.PosterPath)
                        ? "/images/placeholder.png"
                        : GetPosterUrl(x.PosterPath, 185)
                })
                .ToList() ?? new List<TmdbSearchResult>();
        }
        catch
        {
            return new List<TmdbSearchResult>();
        }
    }

    public string GetPosterUrl(string posterPath, int width = 200)
        => $"https://image.tmdb.org/t/p/w{width}{posterPath}";

    public sealed class TmdbSearchResult
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string ReleaseYear { get; set; } = "-";
        public string PosterUrl { get; set; } = "/images/placeholder.png";
    }

    private sealed class TmdbSearchResponse
    {
        [JsonPropertyName("results")]
        public List<TmdbSearchItem> Results { get; set; } = new();
    }

    private sealed class TmdbSearchItem
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("release_date")]
        public string? ReleaseDate { get; set; }

        [JsonPropertyName("poster_path")]
        public string? PosterPath { get; set; }
    }

    private class TmdbDetailResponse
    {
        [JsonPropertyName("poster_path")]
        public string? PosterPath { get; set; }

        [JsonPropertyName("runtime")]
        public int? Runtime { get; set; }

        [JsonPropertyName("episode_run_time")]
        public List<int>? EpisodeRunTime { get; set; }
    }
}
