using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using PrivateLibrary.Data;
using PrivateLibrary.Shared.Auth;

namespace PrivateLibrary.Api.Auth;

/// <summary>
/// Issues short-lived JWT access tokens (carrying the same claims the cookie auth
/// produced — user id, name, roles and household_id) and long-lived, rotating
/// refresh tokens for the WASM client.
/// </summary>
public sealed class JwtTokenService(
    AppDbContext db,
    UserManager<ApplicationUser> userManager,
    IUserClaimsPrincipalFactory<ApplicationUser> claimsFactory,
    JwtOptions options)
{
    private readonly AppDbContext _db = db;
    private readonly UserManager<ApplicationUser> _userManager = userManager;
    private readonly IUserClaimsPrincipalFactory<ApplicationUser> _claimsFactory = claimsFactory;
    private readonly JwtOptions _options = options;

    public async Task<AuthResponse> IssueAsync(ApplicationUser user, CancellationToken ct = default)
    {
        // Reuse the app's claims factory (incl. HouseholdClaimsPrincipalFactory) so the
        // JWT carries exactly the claims CurrentUserContext + services already rely on.
        var principal = await _claimsFactory.CreateAsync(user);
        var claims = principal.Claims.ToList();

        // Ensure the essentials are present and uniquely named for JWT.
        EnsureClaim(claims, ClaimTypes.NameIdentifier, user.Id);
        EnsureClaim(claims, ClaimTypes.Name, user.UserName ?? user.Id);

        var now = DateTime.UtcNow;
        var expires = now.AddMinutes(_options.AccessTokenMinutes);

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var jwt = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: now,
            expires: expires,
            signingCredentials: creds);

        var accessToken = new JwtSecurityTokenHandler().WriteToken(jwt);
        var refreshToken = await CreateRefreshTokenAsync(user.Id, ct);

        var roles = await _userManager.GetRolesAsync(user);

        return new AuthResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            AccessTokenExpiresAt = expires,
            UserId = user.Id,
            UserName = user.UserName ?? string.Empty,
            DisplayName = user.DisplayName,
            HouseholdId = user.HouseholdId,
            Roles = roles.ToArray()
        };
    }

    /// <summary>Rotates a refresh token: validates it, revokes it, and issues a fresh pair.</summary>
    public async Task<AuthResponse?> RefreshAsync(string refreshToken, CancellationToken ct = default)
    {
        var existing = await _db.RefreshTokens
            .FirstOrDefaultAsync(t => t.Token == refreshToken, ct);

        if (existing is null || !existing.IsActive)
        {
            return null;
        }

        existing.RevokedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        var user = await _userManager.FindByIdAsync(existing.UserId);
        if (user is null)
        {
            return null;
        }

        return await IssueAsync(user, ct);
    }

    public async Task RevokeAsync(string refreshToken, CancellationToken ct = default)
    {
        var existing = await _db.RefreshTokens.FirstOrDefaultAsync(t => t.Token == refreshToken, ct);
        if (existing is { RevokedAt: null })
        {
            existing.RevokedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }
    }

    private async Task<string> CreateRefreshTokenAsync(string userId, CancellationToken ct)
    {
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');

        _db.RefreshTokens.Add(new RefreshToken
        {
            UserId = userId,
            Token = token,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(_options.RefreshTokenDays)
        });
        await _db.SaveChangesAsync(ct);
        return token;
    }

    private static void EnsureClaim(List<Claim> claims, string type, string value)
    {
        if (!claims.Any(c => c.Type == type))
        {
            claims.Add(new Claim(type, value));
        }
    }
}

/// <summary>JWT configuration, bound from the "Jwt" config section.</summary>
public sealed class JwtOptions
{
    public string SigningKey { get; set; } = string.Empty;
    public string Issuer { get; set; } = "PrivateLibrary";
    public string Audience { get; set; } = "PrivateLibrary";
    public int AccessTokenMinutes { get; set; } = 30;
    public int RefreshTokenDays { get; set; } = 30;
}
