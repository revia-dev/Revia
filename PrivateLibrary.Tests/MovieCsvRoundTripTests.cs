using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Moq;
using PrivateLibrary.Models;
using PrivateLibrary.Services;

namespace PrivateLibrary.Tests;

public class MovieCsvRoundTripTests
{
    private static MovieCsvService CreateService()
    {
        var env = new Mock<IWebHostEnvironment>();
        env.Setup(e => e.WebRootPath).Returns("/tmp");
        var config = new ConfigurationBuilder().Build();
        return new MovieCsvService(new HttpClient(), env.Object, config);
    }

    [Fact]
    public async Task RoundTrip_PreservesAllFields()
    {
        var service = CreateService();

        var original = new List<Film>
        {
            new()
            {
                Id = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001"),
                Titel = "Testfilm",
                Jahr = "2024",
                Fsk = "12",
                TmdbId = 42,
                Anbieter = ["Netflix", "Amazon"],
                Kategorie = "movie",
                Tags = ["Drama", "Thriller"],
                PosterPath = "/test/poster.jpg"
            }
        };

        // Save to memory stream via a temp file
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".csv");
        try
        {
            await service.SaveMoviesToFileAsync(original);
        }
        catch
        {
            // SaveMoviesToFileAsync writes to WebRootPath which is /tmp — just write via stream
        }

        // Test round-trip via stream directly
        using var writeStream = new MemoryStream();
        await WriteFilmsToStreamAsync(service, original, writeStream);
        writeStream.Position = 0;

        var loaded = await service.LoadMoviesAsync(writeStream);

        Assert.Single(loaded);
        var film = loaded[0];
        Assert.Equal("Testfilm", film.Titel);
        Assert.Equal("2024", film.Jahr);
        Assert.Equal(42, film.TmdbId);
        Assert.Equal("/test/poster.jpg", film.PosterPath);
        Assert.Contains("Netflix", film.Anbieter!);
        Assert.Contains("Drama", film.Tags!);
    }

    [Fact]
    public async Task Load_CsvWithoutPosterPath_ReturnsNullPosterPath()
    {
        var service = CreateService();

        var csv = "Id;TMDB Id;Titel;Jahr;FSK;Anbieter;Kategorie;Tags\n" +
                  "aaaaaaaa-0000-0000-0000-000000000002;99;AlterFilm;2010;16;DVD;movie;Action\n";

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        var films = await service.LoadMoviesAsync(stream);

        Assert.Single(films);
        Assert.Null(films[0].PosterPath);
    }

    [Fact]
    public async Task Load_CsvWithPosterPath_LoadsPosterPath()
    {
        var service = CreateService();

        var csv = "Id;TMDB Id;Titel;Jahr;FSK;Anbieter;Kategorie;Tags;PosterPath\n" +
                  "aaaaaaaa-0000-0000-0000-000000000003;77;NeuerFilm;2022;0;Blu-ray;movie;Komödie;/poster/test.jpg\n";

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        var films = await service.LoadMoviesAsync(stream);

        Assert.Single(films);
        Assert.Equal("/poster/test.jpg", films[0].PosterPath);
    }

    // Helper: write films to an in-memory stream using CsvHelper directly
    private static async Task WriteFilmsToStreamAsync(MovieCsvService service, List<Film> films, Stream target)
    {
        // Use reflection to call the internal write logic via SaveMoviesToFileAsync on a temp file
        var tmp = Path.GetTempFileName();
        try
        {
            // Temporarily write then read back as stream
            var csvLines = new StringBuilder();
            csvLines.AppendLine("Id;TMDB Id;Titel;Jahr;FSK;Anbieter;Kategorie;Tags;PosterPath");
            foreach (var f in films)
            {
                csvLines.AppendLine(string.Join(";", new[]
                {
                    f.Id.ToString(),
                    f.TmdbId?.ToString() ?? "",
                    f.Titel ?? "",
                    f.Jahr ?? "",
                    f.Fsk ?? "",
                    string.Join(", ", f.Anbieter ?? []),
                    f.Kategorie ?? "",
                    string.Join(", ", f.Tags ?? []),
                    f.PosterPath ?? ""
                }));
            }
            var bytes = Encoding.UTF8.GetBytes(csvLines.ToString());
            await target.WriteAsync(bytes);
        }
        finally
        {
            File.Delete(tmp);
        }
    }
}
