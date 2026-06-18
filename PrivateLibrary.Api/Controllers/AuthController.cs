using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PrivateLibrary.Api.Auth;
using PrivateLibrary.Data;
using PrivateLibrary.Shared.Auth;

namespace PrivateLibrary.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(
    UserManager<ApplicationUser> userManager,
    JwtTokenService tokens) : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager = userManager;
    private readonly JwtTokenService _tokens = tokens;

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var user = await _userManager.FindByNameAsync(request.UserName);
        if (user is null || user.IsLoginless)
        {
            return Unauthorized(new { message = "Benutzername oder Passwort ist falsch." });
        }

        if (!await _userManager.CheckPasswordAsync(user, request.Password))
        {
            return Unauthorized(new { message = "Benutzername oder Passwort ist falsch." });
        }

        return await _tokens.IssueAsync(user, ct);
    }

    [AllowAnonymous]
    [HttpPost("refresh")]
    public async Task<ActionResult<AuthResponse>> Refresh([FromBody] RefreshRequest request, CancellationToken ct)
    {
        var result = await _tokens.RefreshAsync(request.RefreshToken, ct);
        return result is null
            ? Unauthorized(new { message = "Sitzung abgelaufen. Bitte erneut anmelden." })
            : result;
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] RefreshRequest request, CancellationToken ct)
    {
        await _tokens.RevokeAsync(request.RefreshToken, ct);
        return NoContent();
    }
}
