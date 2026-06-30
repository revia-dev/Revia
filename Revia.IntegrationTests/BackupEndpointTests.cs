using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Revia.IntegrationTests;

/// <summary>
/// End-to-end coverage of the backup endpoint: it is admin-only, round-trips a movie
/// catalogue, and emits the camelCase field names that form the cross-app contract
/// with the native app.
/// </summary>
public sealed class BackupEndpointTests(ReviaApiFactory factory) : IClassFixture<ReviaApiFactory>
{
    private readonly ReviaApiFactory _factory = factory;

    private sealed record LoginBody(string UserName, string Password);

    [Fact]
    public async Task Backup_WithoutAdmin_IsUnauthorized()
    {
        var client = _factory.CreateClient();
        var res = await client.GetAsync("/api/backup");
        // Never served without admin: auth-rejected (401/403) — or throttled (429) under
        // the edge rate limiter when the suite runs in parallel.
        Assert.NotEqual(HttpStatusCode.OK, res.StatusCode);
        Assert.True(res.StatusCode is HttpStatusCode.Unauthorized
            or HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task Backup_RoundTripsMovies_AndUsesTheSharedSchema()
    {
        var client = _factory.CreateClient();
        var token = await LoginAsAdminAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Restore a backup that carries a single movie (overwrites the household movies).
        var movieId = Guid.NewGuid();
        var document = new
        {
            formatVersion = 1,
            movies = new[]
            {
                new
                {
                    id = movieId,
                    title = "Backup Dune",
                    year = 2021,
                    ageRating = "12",
                    category = "movie",
                    providers = new[] { "Sky" },
                    tags = new[] { "Sci-Fi" },
                    runtimeMinutes = 155,
                    seriesName = "dune",
                    posterPath = "/poster.jpg",
                },
            },
        };
        var post = await client.PostAsJsonAsync("/api/backup", document);
        Assert.Equal(HttpStatusCode.OK, post.StatusCode);
        using (var postDoc = JsonDocument.Parse(await post.Content.ReadAsStringAsync()))
        {
            var overwritten = postDoc.RootElement.GetProperty("overwritten").EnumerateArray()
                .Select(e => e.GetString()).ToList();
            Assert.Contains("movies", overwritten);
        }

        // Export it back and verify the round-trip + the contract field names.
        var raw = await client.GetStringAsync("/api/backup");
        using var doc = JsonDocument.Parse(raw);
        var movies = doc.RootElement.GetProperty("movies");
        var mine = movies.EnumerateArray().Single(m => m.GetProperty("id").GetGuid() == movieId);

        Assert.Equal("Backup Dune", mine.GetProperty("title").GetString());
        Assert.Equal(2021, mine.GetProperty("year").GetInt32());
        Assert.Equal("dune", mine.GetProperty("seriesName").GetString());      // contract key
        Assert.Equal(155, mine.GetProperty("runtimeMinutes").GetInt32());      // contract key
        Assert.Equal("/poster.jpg", mine.GetProperty("posterPath").GetString()); // contract key
        Assert.Equal("Sci-Fi", mine.GetProperty("tags")[0].GetString());
    }

    private static async Task<string> LoginAsAdminAsync(HttpClient client)
    {
        var res = await client.PostAsJsonAsync("/api/auth/login",
            new LoginBody(ReviaApiFactory.AdminUser, ReviaApiFactory.AdminPassword));
        res.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("accessToken").GetString()!;
    }
}
