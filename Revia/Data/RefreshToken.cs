using System.ComponentModel.DataAnnotations;

namespace Revia.Data;

/// <summary>
/// A persisted refresh token for the WASM client's token-based auth. Access tokens
/// are short-lived JWTs; this long-lived token (rotated on each use) lets the client
/// obtain a new access token without re-entering credentials, and supports server-side
/// revocation on logout.
/// </summary>
public sealed class RefreshToken
{
    public int Id { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;

    public ApplicationUser? User { get; set; }

    /// <summary>Opaque random token value (the secret handed to the client).</summary>
    [Required, StringLength(200)]
    public string Token { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }

    public DateTime CreatedAt { get; set; }

    /// <summary>Set when the token is rotated or revoked; a revoked token is unusable.</summary>
    public DateTime? RevokedAt { get; set; }

    public bool IsActive => RevokedAt is null && ExpiresAt > DateTime.UtcNow;
}
