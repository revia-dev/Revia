using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Revia.Api;

namespace Revia.IntegrationTests;

/// <summary>
/// Spins up the real Revia.Api host in-process against an isolated, throwaway SQLite
/// database file, so integration tests exercise the actual middleware pipeline
/// (auth, rate limiting, ProblemDetails, health) end-to-end.
///
/// Configuration is supplied via environment variables set in the static constructor
/// (before any host is built): the minimal-hosting <c>Program</c> reads
/// <c>builder.Configuration</c> at configuration time — before WebApplicationFactory's
/// <c>ConfigureAppConfiguration</c> callbacks run — so env vars (read by
/// <c>CreateBuilder</c>) are the reliable override point. The strong test key and
/// non-default admin password also prove the Production-grade startup validation
/// passes for a properly configured deployment.
/// </summary>
public sealed class ReviaApiFactory : WebApplicationFactory<ApiAssemblyMarker>
{
    public const string AdminUser = "admin";
    public const string AdminPassword = "Integr@tionTest-Admin-Pw-9999";

    /// <summary>Auth rate-limit permit/minute for this host. Relaxed by default so the
    /// many logins across the suite don't exhaust the window (a flaky 429); the
    /// dedicated rate-limit test sets it low to assert throttling.</summary>
    public int AuthPermitLimit { get; init; } = 1000;
    public bool DemoEnabled { get; init; }

    static ReviaApiFactory()
    {
        // A single throwaway DB shared by all hosts in the run; the seeder is
        // idempotent, so multiple hosts opening it is harmless. The OS temp sweeper
        // reclaims the file (we don't delete it, to avoid one host removing another's).
        var dbPath = Path.Combine(Path.GetTempPath(), $"revia-itest-{Guid.NewGuid():N}.db");

        Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", $"Data Source={dbPath}");
        Environment.SetEnvironmentVariable("Jwt__SigningKey", "integration-test-signing-key-which-is-definitely-long-enough-123456");
        Environment.SetEnvironmentVariable("Seed__AdminPassword", AdminPassword);
        Environment.SetEnvironmentVariable("Demo__Enabled", "false");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Run as Production so the host mirrors a real deployment (no dev shortcuts).
        builder.UseEnvironment("Production");
        builder.UseSetting("RateLimit:AuthPermitLimit", AuthPermitLimit.ToString());
        builder.UseSetting("Demo:Enabled", DemoEnabled.ToString());
    }
}
