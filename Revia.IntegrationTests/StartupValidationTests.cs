using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Revia.Api.Auth;
using Revia.Api.Infrastructure;

namespace Revia.IntegrationTests;

/// <summary>
/// Verifies the fail-fast JWT configuration rules directly without starting a host.
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

    private const string StrongKey = "this-is-a-strong-enough-signing-key-1234567890";

    [Fact]
    public void Production_MissingKey_Throws()
    {
        var jwt = new JwtOptions { SigningKey = "" };
        Assert.Throws<InvalidOperationException>(() =>
            StartupValidation.ValidateAndHardenJwt(jwt, new FakeEnv("Production")));
    }

    [Fact]
    public void Production_ShortKey_Throws()
    {
        var jwt = new JwtOptions { SigningKey = "too-short" };
        Assert.Throws<InvalidOperationException>(() =>
            StartupValidation.ValidateAndHardenJwt(jwt, new FakeEnv("Production")));
    }

    [Fact]
    public void Production_StrongKey_DoesNotThrow()
    {
        var jwt = new JwtOptions { SigningKey = StrongKey };
        StartupValidation.ValidateAndHardenJwt(jwt, new FakeEnv("Production"));
        Assert.Equal(StrongKey, jwt.SigningKey);
    }

    [Fact]
    public void Development_MissingKey_GetsDevFallback()
    {
        var jwt = new JwtOptions { SigningKey = "" };
        StartupValidation.ValidateAndHardenJwt(jwt, new FakeEnv("Development"));
        Assert.True(jwt.SigningKey.Length >= StartupValidation.MinSigningKeyLength);
    }
}
