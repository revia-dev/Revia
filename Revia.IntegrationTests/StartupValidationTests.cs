using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Revia.Api.Auth;
using Revia.Api.Infrastructure;

namespace Revia.IntegrationTests;

/// <summary>
/// Verifies the fail-fast config rules directly (no host needed): the JWT signing key
/// is hard-validated in Production, while the default admin password is only a warning
/// — so the documented out-of-the-box Docker/LAN flow (Production + default password)
/// still starts.
/// </summary>
public sealed class StartupValidationTests
{
    private sealed class FakeEnv(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "Revia.Api";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private static IConfiguration Config(params (string key, string value)[] values)
        => new ConfigurationBuilder()
            .AddInMemoryCollection(values.Select(v =>
                new KeyValuePair<string, string?>(v.key, v.value)))
            .Build();

    private const string StrongKey = "this-is-a-strong-enough-signing-key-1234567890";

    [Fact]
    public void Production_MissingKey_Throws()
    {
        var jwt = new JwtOptions { SigningKey = "" };
        Assert.Throws<InvalidOperationException>(() =>
            StartupValidation.ValidateAndHardenJwt(jwt, new FakeEnv("Production"), Config()));
    }

    [Fact]
    public void Production_ShortKey_Throws()
    {
        var jwt = new JwtOptions { SigningKey = "too-short" };
        Assert.Throws<InvalidOperationException>(() =>
            StartupValidation.ValidateAndHardenJwt(jwt, new FakeEnv("Production"), Config()));
    }

    [Fact]
    public void Production_StrongKey_DefaultAdminPassword_DoesNotThrow()
    {
        // This is the Docker/LAN out-of-the-box case: it must still start.
        var jwt = new JwtOptions { SigningKey = StrongKey };
        var cfg = Config(("Seed:AdminPassword", StartupValidation.DefaultAdminPassword));
        StartupValidation.ValidateAndHardenJwt(jwt, new FakeEnv("Production"), cfg);
        Assert.True(StartupValidation.UsesDefaultAdminPassword(new FakeEnv("Production"), cfg));
    }

    [Fact]
    public void Development_MissingKey_GetsDevFallback()
    {
        var jwt = new JwtOptions { SigningKey = "" };
        StartupValidation.ValidateAndHardenJwt(jwt, new FakeEnv("Development"), Config());
        Assert.True(jwt.SigningKey.Length >= StartupValidation.MinSigningKeyLength);
    }

    [Fact]
    public void Development_DefaultAdminPassword_NotFlagged()
    {
        var cfg = Config(("Seed:AdminPassword", StartupValidation.DefaultAdminPassword));
        Assert.False(StartupValidation.UsesDefaultAdminPassword(new FakeEnv("Development"), cfg));
    }
}
