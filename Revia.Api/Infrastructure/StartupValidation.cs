using Revia.Api.Auth;

namespace Revia.Api.Infrastructure;

/// <summary>
/// Fail-fast configuration validation run at startup. The principle: a misconfigured
/// production deployment must refuse to start rather than come up silently insecure.
/// In Development we stay convenient (auto-generate a key); in Production we hard-fail
/// on the catastrophic case — a missing/weak token-signing key — because that would
/// make every issued token forgeable. The weak default admin password is a softer
/// risk (it can be changed after login) and would break the documented out-of-the-box
/// Docker/LAN flow if it hard-failed, so it is surfaced as a loud warning instead
/// (see <see cref="UsesDefaultAdminPassword"/>).
/// </summary>
public static class StartupValidation
{
    /// <summary>Minimum signing-key length. HMAC-SHA256 keys shorter than the 256-bit
    /// hash add no security and are a common foot-gun.</summary>
    public const int MinSigningKeyLength = 32;

    /// <summary>The seeded default admin password shipped in appsettings.</summary>
    public const string DefaultAdminPassword = "Admin123!";

    public static void ValidateAndHardenJwt(
        JwtOptions jwt, IHostEnvironment env, IConfiguration configuration)
    {
        if (string.IsNullOrWhiteSpace(jwt.SigningKey))
        {
            if (env.IsDevelopment())
            {
                // Convenience only: run out of the box locally. Never used in Production.
                jwt.SigningKey = "dev-only-insecure-signing-key-change-me-32+chars-please!!";
            }
            else
            {
                throw new InvalidOperationException(
                    "Jwt:SigningKey is not configured. Set a strong secret (>= " +
                    $"{MinSigningKeyLength} chars) via environment variable Jwt__SigningKey " +
                    "or your secret store before starting in Production.");
            }
        }

        if (!env.IsDevelopment() && jwt.SigningKey.Length < MinSigningKeyLength)
        {
            throw new InvalidOperationException(
                $"Jwt:SigningKey is too short ({jwt.SigningKey.Length} chars); " +
                $"use at least {MinSigningKeyLength} characters in Production.");
        }
    }

    /// <summary>True when running outside Development with the shipped default admin
    /// password still in place — worth a startup warning so an operator changes it.</summary>
    public static bool UsesDefaultAdminPassword(IHostEnvironment env, IConfiguration configuration)
        => !env.IsDevelopment()
            && string.Equals(configuration["Seed:AdminPassword"], DefaultAdminPassword, StringComparison.Ordinal);
}
