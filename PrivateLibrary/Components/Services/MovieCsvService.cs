using System.Globalization;
using CsvHelper;
using PrivateLibrary.Models;

namespace PrivateLibrary.Services;

public class MovieCsvService
{
    private readonly HttpClient _http;

    public MovieCsvService(HttpClient http)
    {
        _http = http;
    }

    public async Task<List<Film>> LoadMoviesFromWwwrootAsync()
    {
        await using var stream = await _http.GetStreamAsync("data/movies.csv");
        return await LoadMoviesAsync(stream);
    }

    public async Task<List<Film>> LoadMoviesAsync(Stream csvStream)
    {
        using var reader = new StreamReader(csvStream);
        using var csv = new CsvReader(reader, new CsvHelper.Configuration.CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Delimiter = ";",
            BadDataFound = null,
            MissingFieldFound = null,
            HeaderValidated = null
        });

        var result = new List<Film>();

        await csv.ReadAsync();
        csv.ReadHeader();

        while (await csv.ReadAsync())
        {
            var tmdbIdString = csv.GetField("TMDB Id");
            if (!int.TryParse(tmdbIdString, out var tmdbId))
                continue;

            var titel = csv.GetField("Titel");
            var jahr = csv.GetField("Jahr");
            var fsk = csv.GetField("FSK");
            var anbieterString = csv.GetField("Anbieter") ?? string.Empty;
            var kategorie = csv.GetField("Kategorie");
            var tagsString = csv.GetField("Tags") ?? string.Empty;

            if (!string.IsNullOrWhiteSpace(fsk) &&
                MovieMappings.UsToFskMapping.TryGetValue(fsk, out var mappedFsk))
            {
                fsk = mappedFsk;
            }

            var rawTags = tagsString
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            var rawAnbieter = anbieterString
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            var cleanedTags = MovieMappings.NormalizeTags(rawTags);
            var cleanedAnbieter = MovieMappings.NormalizeTags(rawAnbieter);

            result.Add(new Film
            {
                Titel = titel,
                Jahr = jahr,
                TmdbId = tmdbId,
                Fsk = string.IsNullOrWhiteSpace(fsk) ? null : fsk,
                Anbieter = cleanedAnbieter,
                Kategorie = kategorie,
                Tags = cleanedTags
            });
        }

        return result
            .OrderBy(f => f.Titel, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
