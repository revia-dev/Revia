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

    [Fact]
    public async Task Backup_WatchProgressAndBmi_ReconcileByPersonName()
    {
        var client = _factory.CreateClient();
        var token = await LoginAsAdminAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // A named, loginless person so name-resolution has a real target (the admin has
        // an empty display name).
        var personName = "Backup Tester " + Guid.NewGuid().ToString("N")[..6];
        var createPerson = await client.PostAsJsonAsync("/api/persons", new { firstName = personName });
        createPerson.EnsureSuccessStatusCode();

        var movieId = Guid.NewGuid();
        var document = new
        {
            formatVersion = 1,
            movies = new[] { new { id = movieId, title = "WP Movie", category = "movie",
                                   providers = Array.Empty<string>(), tags = Array.Empty<string>() } },
            watchProgress = new[]
            {
                // foreign personId (native-style UUID) — must re-attach by name
                new { id = Guid.NewGuid(), personId = Guid.NewGuid(), personName,
                      movieId, status = "watched", season = (int?)null, episode = (int?)null,
                      positionSeconds = (int?)null, updatedAt = DateTime.UtcNow },
                // a person who doesn't exist here → dropped, not mis-attached
                new { id = Guid.NewGuid(), personId = Guid.NewGuid(), personName = "Ghost Nobody",
                      movieId, status = "watched", season = (int?)null, episode = (int?)null,
                      positionSeconds = (int?)null, updatedAt = DateTime.UtcNow },
            },
            bmiRecords = new[]
            {
                new { id = Guid.NewGuid(), personId = Guid.NewGuid(), personName,
                      measuredOn = DateTime.UtcNow, weightKg = 70.0, heightCm = 175.0 },
            },
        };
        var post = await client.PostAsJsonAsync("/api/backup", document);
        Assert.Equal(HttpStatusCode.OK, post.StatusCode);

        var raw = await client.GetStringAsync("/api/backup");
        using var doc = JsonDocument.Parse(raw);

        var progress = doc.RootElement.GetProperty("watchProgress").EnumerateArray().ToList();
        // The named person's row survived and re-attached; the ghost row was dropped.
        Assert.Contains(progress, p => p.GetProperty("personName").GetString() == personName
                                       && p.GetProperty("movieId").GetGuid() == movieId
                                       && p.GetProperty("status").GetString() == "watched");
        Assert.DoesNotContain(progress, p => p.GetProperty("personName").GetString() == "Ghost Nobody");

        var bmi = doc.RootElement.GetProperty("bmiRecords").EnumerateArray().ToList();
        Assert.Contains(bmi, b => b.GetProperty("personName").GetString() == personName
                                  && Math.Abs(b.GetProperty("weightKg").GetDouble() - 70.0) < 0.001);
    }

    [Fact]
    public async Task Backup_LoansAndMatches_ReconcileByName()
    {
        var client = _factory.CreateClient();
        var token = await LoginAsAdminAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var suffix = Guid.NewGuid().ToString("N")[..6];
        var lender = "Lender " + suffix;
        var borrower = "Borrower " + suffix;
        var gameName = "Catan " + suffix;

        (await client.PostAsJsonAsync("/api/persons", new { firstName = lender })).EnsureSuccessStatusCode();
        (await client.PostAsJsonAsync("/api/persons", new { firstName = borrower })).EnsureSuccessStatusCode();
        var gameRes = await client.PostAsJsonAsync("/api/games",
            new { name = gameName, types = Array.Empty<object>(), scoreDefinitions = Array.Empty<object>() });
        gameRes.EnsureSuccessStatusCode();

        var document = new
        {
            formatVersion = 1,
            loans = new[]
            {
                new { id = Guid.NewGuid(), title = "Drill", items = new[] { "Drill", "Bits" },
                      lenderPersonId = Guid.NewGuid(), lenderName = lender,
                      borrowerPersonId = Guid.NewGuid(), borrowerName = borrower,
                      loanDate = DateTime.UtcNow, dueDate = (DateTime?)null, returnedAt = (DateTime?)null },
                // unresolvable people → dropped (web FK needs real users)
                new { id = Guid.NewGuid(), title = "Ghost loan", items = new[] { "x" },
                      lenderPersonId = Guid.NewGuid(), lenderName = "Nobody A",
                      borrowerPersonId = Guid.NewGuid(), borrowerName = "Nobody B",
                      loanDate = DateTime.UtcNow, dueDate = (DateTime?)null, returnedAt = (DateTime?)null },
            },
            matches = new[]
            {
                new { id = Guid.NewGuid(), gameId = Guid.NewGuid(), gameName,
                      playedOn = DateTime.UtcNow, note = "gg",
                      participants = new[] { new { id = Guid.NewGuid(), personId = Guid.NewGuid(),
                                                   personName = lender, score = 5.0 } } },
                // unknown game → dropped (same shape so the anonymous array has one type)
                new { id = Guid.NewGuid(), gameId = Guid.NewGuid(), gameName = "No Such Game " + suffix,
                      playedOn = DateTime.UtcNow, note = "x",
                      participants = new[] { new { id = Guid.NewGuid(), personId = Guid.NewGuid(),
                                                   personName = lender, score = 1.0 } } },
            },
        };
        var post = await client.PostAsJsonAsync("/api/backup", document);
        Assert.Equal(HttpStatusCode.OK, post.StatusCode);

        var raw = await client.GetStringAsync("/api/backup");
        using var doc = JsonDocument.Parse(raw);

        var loans = doc.RootElement.GetProperty("loans").EnumerateArray().ToList();
        var loan = loans.Single(l => l.GetProperty("lenderName").GetString() == lender);
        Assert.Equal(borrower, loan.GetProperty("borrowerName").GetString());
        Assert.Contains("Drill", loan.GetProperty("items").EnumerateArray().Select(i => i.GetString()));
        Assert.DoesNotContain(loans, l => l.GetProperty("lenderName").GetString() == "Nobody A");

        var matches = doc.RootElement.GetProperty("matches").EnumerateArray().ToList();
        var match = matches.Single(m => m.GetProperty("gameName").GetString() == gameName);
        var participant = match.GetProperty("participants").EnumerateArray().Single();
        Assert.Equal(lender, participant.GetProperty("personName").GetString());
        Assert.Equal(5.0, participant.GetProperty("score").GetDouble(), 3);
        Assert.DoesNotContain(matches, m => m.GetProperty("gameName").GetString() == "No Such Game " + suffix);
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
