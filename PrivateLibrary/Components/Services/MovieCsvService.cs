using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using PrivateLibrary.Models;

namespace PrivateLibrary.Services;

public class MovieCsvService
{
    private readonly HttpClient _http;
    private readonly IWebHostEnvironment _environment;
    private readonly IConfiguration _configuration;

    public MovieCsvService(HttpClient http, IWebHostEnvironment environment, IConfiguration configuration)
    {
        _http = http;
        _environment = environment;
        _configuration = configuration;
    }

    public async Task<List<Film>> LoadMoviesFromWwwrootAsync()
    {
        await using var stream = await _http.GetStreamAsync("data/movies.csv");
        return await LoadMoviesAsync(stream);
    }

    public async Task<List<Film>> LoadMoviesFromFileAsync()
    {
        var path = GetCsvPath();
        await using var stream = File.OpenRead(path);
        return await LoadMoviesAsync(stream);
    }

    public async Task SaveMoviesToFileAsync(IEnumerable<Film> films)
    {
        var path = GetCsvPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        await using var stream = File.Create(path);
        await using var writer = new StreamWriter(stream, new UTF8Encoding(true));
        await using var csv = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Delimiter = ";"
        });

        csv.WriteField("Id");
        csv.WriteField("TMDB Id");
        csv.WriteField("Titel");
        csv.WriteField("Jahr");
        csv.WriteField("FSK");
        csv.WriteField("Anbieter");
        csv.WriteField("Kategorie");
        csv.WriteField("Tags");
        csv.WriteField("PosterPath");
        await csv.NextRecordAsync();

        foreach (var film in films.OrderBy(x => x.Titel, StringComparer.OrdinalIgnoreCase))
        {
            csv.WriteField(film.Id.ToString());
            csv.WriteField(film.TmdbId?.ToString() ?? string.Empty);
            csv.WriteField(film.Titel ?? string.Empty);
            csv.WriteField(film.Jahr ?? string.Empty);
            csv.WriteField(film.Fsk ?? string.Empty);
            csv.WriteField(string.Join(", ", (film.Anbieter ?? []).Where(x => !string.IsNullOrWhiteSpace(x))));
            csv.WriteField(film.Kategorie ?? string.Empty);
            csv.WriteField(string.Join(", ", (film.Tags ?? []).Where(x => !string.IsNullOrWhiteSpace(x))));
            csv.WriteField(film.PosterPath ?? string.Empty);
            await csv.NextRecordAsync();
        }

        await writer.FlushAsync();
    }

    public async Task<List<Film>> LoadMoviesAsync(Stream csvStream)
    {
        using var reader = new StreamReader(csvStream);
        using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
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
            var idString = TryGetField(csv, "Id");
            var tmdbIdString = csv.GetField("TMDB Id");
            var id = Guid.TryParse(idString, out var parsedId) ? parsedId : Guid.NewGuid();
            int? tmdbId = int.TryParse(tmdbIdString, out var parsedTmdbId) ? parsedTmdbId : null;

            var titel = csv.GetField("Titel");
            var jahr = csv.GetField("Jahr");
            var fsk = csv.GetField("FSK");
            var anbieterString = csv.GetField("Anbieter") ?? string.Empty;
            var kategorie = csv.GetField("Kategorie");
            var tagsString = csv.GetField("Tags") ?? string.Empty;
            var posterPath = TryGetField(csv, "PosterPath");

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
                Id = id,
                Titel = titel,
                Jahr = jahr,
                TmdbId = tmdbId,
                Fsk = string.IsNullOrWhiteSpace(fsk) ? null : fsk,
                Anbieter = cleanedAnbieter,
                Kategorie = kategorie,
                Tags = cleanedTags,
                PosterPath = string.IsNullOrWhiteSpace(posterPath) ? null : posterPath
            });
        }

        return result
            .OrderBy(f => f.Titel, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private string GetCsvPath()
    {
        var configured = _configuration["Movies:CsvPath"];
        return !string.IsNullOrWhiteSpace(configured)
            ? configured
            : Path.Combine(_environment.WebRootPath, "data", "movies.csv");
    }

    private static string? TryGetField(CsvReader csv, string fieldName)
    {
        try { return csv.GetField(fieldName); }
        catch { return null; }
    }
}
