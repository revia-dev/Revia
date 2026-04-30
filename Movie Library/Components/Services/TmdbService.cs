using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace MovieLibraryApp.Services;

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
            return response?.PosterPath;
        }
        catch
        {
            return null;
        }
    }

    public string GetPosterUrl(string posterPath, int width = 200)
        => $"https://image.tmdb.org/t/p/w{width}{posterPath}";

    private class TmdbDetailResponse
    {
        [JsonPropertyName("poster_path")]
        public string? PosterPath { get; set; }
    }
}
