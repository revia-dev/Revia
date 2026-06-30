using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Revia.IntegrationTests;

/// <summary>
/// End-to-end coverage of the per-user watch-progress endpoint: it is protected, and
/// it round-trips an upsert (create a movie, set progress, read it back) over the
/// real HTTP pipeline.
/// </summary>
public sealed class ProgressEndpointTests(ReviaApiFactory factory) : IClassFixture<ReviaApiFactory>
{
    private readonly ReviaApiFactory _factory = factory;

    private sealed record LoginBody(string UserName, string Password);

    [Fact]
    public async Task Progress_WithoutToken_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();
        var res = await client.GetAsync("/api/progress");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Progress_RoundTrips_ForSignedInUser()
    {
        var client = _factory.CreateClient();
        var token = await LoginAsAdminAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Admins may add movies; create one to attach progress to.
        var movieId = Guid.NewGuid();
        var create = await client.PostAsJsonAsync("/api/movies", new
        {
            Id = movieId,
            Titel = "Integration Dune",
            Kategorie = "movie",
        });
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);

        // Upsert progress for that movie.
        var put = await client.PutAsJsonAsync("/api/progress", new
        {
            MovieId = movieId,
            Status = "inProgress",
            PositionSeconds = 1234,
        });
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);

        // It must come back in the user's progress list with the saved values.
        var list = await client.GetFromJsonAsync<List<JsonElement>>("/api/progress");
        Assert.NotNull(list);
        var mine = list!.FirstOrDefault(e =>
            e.GetProperty("movieId").GetGuid() == movieId);
        Assert.Equal("inProgress", mine.GetProperty("status").GetString());
        Assert.Equal(1234, mine.GetProperty("positionSeconds").GetInt32());
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
