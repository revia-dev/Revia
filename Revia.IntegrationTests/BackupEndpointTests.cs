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

    [Fact]
    public async Task Backup_Wishlists_ResolveOwnerByNameWithImporterFallback()
    {
        var client = _factory.CreateClient();
        var token = await LoginAsAdminAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var owner = "Owner " + Guid.NewGuid().ToString("N")[..6];
        (await client.PostAsJsonAsync("/api/persons", new { firstName = owner })).EnsureSuccessStatusCode();

        var ownedName = "Gifts " + Guid.NewGuid().ToString("N")[..6];
        var ownerlessName = "Household " + Guid.NewGuid().ToString("N")[..6];
        var document = new
        {
            formatVersion = 1,
            wishlists = new[]
            {
                new { id = Guid.NewGuid(), householdId = Guid.Empty, name = ownedName,
                      createdAt = DateTime.UtcNow, ownerName = (string?)owner,
                      items = new[] { new { id = Guid.NewGuid(), title = "Book",
                                            note = (string?)"hardcover", url = (string?)"https://x", fulfilled = false } } },
                // no owner (native-style) → the importing admin becomes the owner
                new { id = Guid.NewGuid(), householdId = Guid.Empty, name = ownerlessName,
                      createdAt = DateTime.UtcNow, ownerName = (string?)null,
                      items = new[] { new { id = Guid.NewGuid(), title = "Socks",
                                            note = (string?)null, url = (string?)null, fulfilled = true } } },
            },
        };
        var post = await client.PostAsJsonAsync("/api/backup", document);
        Assert.Equal(HttpStatusCode.OK, post.StatusCode);

        var raw = await client.GetStringAsync("/api/backup");
        using var doc = JsonDocument.Parse(raw);
        var lists = doc.RootElement.GetProperty("wishlists").EnumerateArray().ToList();

        var owned = lists.Single(w => w.GetProperty("name").GetString() == ownedName);
        Assert.Equal(owner, owned.GetProperty("ownerName").GetString());
        var item = owned.GetProperty("items").EnumerateArray().Single();
        Assert.Equal("Book", item.GetProperty("title").GetString());
        Assert.Equal("https://x", item.GetProperty("url").GetString());

        // The ownerless list survived (assigned to the importer), with its item.
        var household = lists.Single(w => w.GetProperty("name").GetString() == ownerlessName);
        Assert.True(household.GetProperty("items").EnumerateArray()
            .Single().GetProperty("fulfilled").GetBoolean());
    }

    [Fact]
    public async Task Backup_TvAirings_RebuildProgramsAndRequireKnownMovie()
    {
        var client = _factory.CreateClient();
        var token = await LoginAsAdminAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var owner = "TV Owner " + Guid.NewGuid().ToString("N")[..6];
        (await client.PostAsJsonAsync("/api/persons", new { firstName = owner })).EnsureSuccessStatusCode();

        var movieId = Guid.NewGuid();
        var programName = "Krimi " + Guid.NewGuid().ToString("N")[..6];
        var document = new
        {
            formatVersion = 1,
            // the movie is restored before TV, so the entry's movie exists
            movies = new[] { new { id = movieId, title = "Tatort", category = "tv",
                                   providers = Array.Empty<string>(), tags = Array.Empty<string>() } },
            tvAirings = new[]
            {
                new { id = Guid.NewGuid(), movieId = (Guid?)movieId, customTitle = (string?)null,
                      channel = (string?)"ARD", note = (string?)"record", startsAt = DateTime.UtcNow,
                      scope = "family", ownerName = (string?)owner, programName = (string?)programName },
                // unknown movie → its program has no showable entry → dropped
                new { id = Guid.NewGuid(), movieId = (Guid?)Guid.NewGuid(), customTitle = (string?)null,
                      channel = (string?)"ZDF", note = (string?)null, startsAt = DateTime.UtcNow,
                      scope = "privateProgram", ownerName = (string?)null,
                      programName = (string?)("Ghost " + Guid.NewGuid().ToString("N")[..6]) },
            },
        };
        var post = await client.PostAsJsonAsync("/api/backup", document);
        Assert.Equal(HttpStatusCode.OK, post.StatusCode);

        var raw = await client.GetStringAsync("/api/backup");
        using var doc = JsonDocument.Parse(raw);
        var airings = doc.RootElement.GetProperty("tvAirings").EnumerateArray().ToList();

        var mine = airings.Single(a => a.GetProperty("programName").GetString() == programName);
        Assert.Equal(movieId, mine.GetProperty("movieId").GetGuid());
        Assert.Equal("family", mine.GetProperty("scope").GetString());
        Assert.Equal("ARD", mine.GetProperty("channel").GetString());
        Assert.Equal(owner, mine.GetProperty("ownerName").GetString());
        // the unknown-movie program was not created
        Assert.DoesNotContain(airings, a =>
            (a.GetProperty("programName").GetString() ?? "").StartsWith("Ghost"));
    }

    [Fact]
    public async Task Backup_Games_RoundTripWithFilesAndUpsertByName()
    {
        var client = _factory.CreateClient();
        var token = await LoginAsAdminAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var gameName = "Catan " + Guid.NewGuid().ToString("N")[..6];
        var rules = new byte[] { 1, 2, 3, 4 };
        var mapFile = new byte[] { 9, 8, 7 };
        var document = new
        {
            formatVersion = 1,
            games = new[]
            {
                new
                {
                    id = Guid.NewGuid(), name = gameName, publisher = "Kosmos",
                    minPlayers = 3, maxPlayers = 4, types = new[] { "board", "card" },
                    hasRules = true, rulesData = rules, rulesFileName = "rules.pdf",
                    scoreDefinitions = new[] { new { id = Guid.NewGuid(), name = "Points",
                                                     unit = (string?)null, higherIsBetter = true } },
                    expansions = new[]
                    {
                        new { id = Guid.NewGuid(), name = "Seafarers", notes = "ocean",
                              files = new[] { new { id = Guid.NewGuid(), fileName = "map.pdf",
                                                    contentType = "application/pdf", data = mapFile } } },
                    },
                },
            },
        };
        (await client.PostAsJsonAsync("/api/backup", document)).EnsureSuccessStatusCode();

        var raw = await client.GetStringAsync("/api/backup");
        using (var doc = JsonDocument.Parse(raw))
        {
            var game = doc.RootElement.GetProperty("games").EnumerateArray()
                .Single(g => g.GetProperty("name").GetString() == gameName);
            Assert.Equal("Kosmos", game.GetProperty("publisher").GetString());
            var types = game.GetProperty("types").EnumerateArray().Select(t => t.GetString()).ToList();
            Assert.Contains("board", types);
            Assert.Contains("card", types);  // German enum mapped back to native strings
            Assert.Equal(rules, game.GetProperty("rulesData").GetBytesFromBase64()); // file round-trip
            Assert.Equal("Points", game.GetProperty("scoreDefinitions")[0].GetProperty("name").GetString());
            var exp = game.GetProperty("expansions")[0];
            Assert.Equal("Seafarers", exp.GetProperty("name").GetString());
            Assert.Equal(mapFile, exp.GetProperty("files")[0].GetProperty("data").GetBytesFromBase64());
        }

        // Re-import the same name with a changed publisher → upsert, not a duplicate.
        var update = new { formatVersion = 1, games = new[] { new
        {
            id = Guid.NewGuid(), name = gameName, publisher = "Ravensburger",
            minPlayers = 2, maxPlayers = 5, types = new[] { "other" },
            hasRules = false, rulesData = (byte[]?)null, rulesFileName = (string?)null,
            scoreDefinitions = Array.Empty<object>(), expansions = Array.Empty<object>(),
        } } };
        (await client.PostAsJsonAsync("/api/backup", update)).EnsureSuccessStatusCode();

        using (var doc = JsonDocument.Parse(await client.GetStringAsync("/api/backup")))
        {
            var matching = doc.RootElement.GetProperty("games").EnumerateArray()
                .Where(g => g.GetProperty("name").GetString() == gameName).ToList();
            Assert.Single(matching);
            Assert.Equal("Ravensburger", matching[0].GetProperty("publisher").GetString());
        }
    }

    [Fact]
    public async Task Backup_LoanAndMatchPhotos_RoundTrip()
    {
        var client = _factory.CreateClient();
        var token = await LoginAsAdminAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var suffix = Guid.NewGuid().ToString("N")[..6];
        var lender = "PL " + suffix;
        var borrower = "PB " + suffix;
        var gameName = "PG " + suffix;
        (await client.PostAsJsonAsync("/api/persons", new { firstName = lender })).EnsureSuccessStatusCode();
        (await client.PostAsJsonAsync("/api/persons", new { firstName = borrower })).EnsureSuccessStatusCode();
        (await client.PostAsJsonAsync("/api/games", new { name = gameName,
            types = Array.Empty<object>(), scoreDefinitions = Array.Empty<object>() })).EnsureSuccessStatusCode();

        var loanPhoto = new byte[] { 10, 20, 30, 40 };
        var matchPhoto = new byte[] { 50, 60, 70 };
        var document = new
        {
            formatVersion = 1,
            loans = new[] { new { id = Guid.NewGuid(), title = "Cam", items = new[] { "Cam" },
                lenderPersonId = Guid.NewGuid(), lenderName = lender,
                borrowerPersonId = Guid.NewGuid(), borrowerName = borrower,
                loanDate = DateTime.UtcNow, dueDate = (DateTime?)null, returnedAt = (DateTime?)null,
                photoData = loanPhoto } },
            matches = new[] { new { id = Guid.NewGuid(), gameId = Guid.NewGuid(), gameName,
                playedOn = DateTime.UtcNow, note = (string?)null, photoData = matchPhoto,
                participants = new[] { new { id = Guid.NewGuid(), personId = Guid.NewGuid(),
                    personName = lender, score = 1.0 } } } },
        };
        (await client.PostAsJsonAsync("/api/backup", document)).EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await client.GetStringAsync("/api/backup"));
        var loan = doc.RootElement.GetProperty("loans").EnumerateArray()
            .Single(l => l.GetProperty("lenderName").GetString() == lender);
        Assert.Equal(loanPhoto, loan.GetProperty("photoData").GetBytesFromBase64());

        var match = doc.RootElement.GetProperty("matches").EnumerateArray()
            .Single(m => m.GetProperty("gameName").GetString() == gameName);
        Assert.Equal(matchPhoto, match.GetProperty("photoData").GetBytesFromBase64());
    }

    [Fact]
    public async Task Tv_CustomTitleEntry_WithoutMovie_IsAcceptedAndShown()
    {
        var client = _factory.CreateClient();
        var token = await LoginAsAdminAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var title = "Tagesschau " + Guid.NewGuid().ToString("N")[..6];
        var add = await client.PostAsJsonAsync("/api/tv", new
        {
            movieId = (Guid?)null,
            customTitle = title,
            startsAt = DateTime.UtcNow.AddHours(1),
            channel = "ARD",
            shared = false,
            household = false,
        });
        Assert.Equal(HttpStatusCode.NoContent, add.StatusCode);

        // The private guide lists it, titled by the custom title, with no movie link.
        using var doc = JsonDocument.Parse(await client.GetStringAsync("/api/tv/private"));
        var entry = doc.RootElement.EnumerateArray()
            .Single(e => e.GetProperty("movieTitle").GetString() == title);
        Assert.Equal(title, entry.GetProperty("customTitle").GetString());
        Assert.True(entry.GetProperty("movieId").ValueKind == JsonValueKind.Null);
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
