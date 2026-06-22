using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Revia.IntegrationTests;

/// <summary>
/// End-to-end tests over the real HTTP pipeline: health probes, the login flow,
/// edge rate limiting, and the ProblemDetails contract. These complement the
/// SyncEngine unit tests by covering what only shows up when the whole host runs.
/// </summary>
public sealed class AuthAndHealthTests(ReviaApiFactory factory) : IClassFixture<ReviaApiFactory>
{
    private readonly ReviaApiFactory _factory = factory;

    private sealed record LoginBody(string UserName, string Password);

    [Fact]
    public async Task Health_Liveness_ReturnsHealthy()
    {
        var client = _factory.CreateClient();
        var res = await client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [Fact]
    public async Task Health_Ready_ChecksDatabase_ReturnsHealthy()
    {
        var client = _factory.CreateClient();
        var res = await client.GetAsync("/health/ready");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [Fact]
    public async Task Ping_ReturnsOk()
    {
        var client = _factory.CreateClient();
        var res = await client.GetAsync("/api/ping");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [Fact]
    public async Task Login_WithSeededAdmin_ReturnsTokens()
    {
        var client = _factory.CreateClient();
        var res = await client.PostAsJsonAsync("/api/auth/login",
            new LoginBody(ReviaApiFactory.AdminUser, ReviaApiFactory.AdminPassword));

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        var root = doc.RootElement;
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("accessToken").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("refreshToken").GetString()));
    }

    [Fact]
    public async Task Login_WithWrongPassword_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();
        var res = await client.PostAsJsonAsync("/api/auth/login",
            new LoginBody(ReviaApiFactory.AdminUser, "definitely-wrong"));

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithoutToken_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();
        // Logout requires [Authorize]; without a bearer token it must be rejected.
        var res = await client.PostAsJsonAsync("/api/auth/logout", new { refreshToken = "x" });
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Login_RateLimited_AfterBurst_Returns429()
    {
        // The "auth" fixed-window limiter permits 10/min; a burst beyond that must be
        // throttled at the edge (independently of per-account lockout). Uses its own
        // host so exhausting the window can't make the other auth tests flaky.
        using var factory = new ReviaApiFactory();
        var client = factory.CreateClient();
        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 15; i++)
        {
            var res = await client.PostAsJsonAsync("/api/auth/login",
                new LoginBody("nobody", "nope"));
            statuses.Add(res.StatusCode);
        }

        Assert.Contains(HttpStatusCode.TooManyRequests, statuses);
    }
}
