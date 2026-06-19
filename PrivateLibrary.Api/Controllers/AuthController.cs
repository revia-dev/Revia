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

    private const string InvalidCredentials = "Benutzername oder Passwort ist falsch.";

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var user = await _userManager.FindByNameAsync(request.UserName);
        if (user is null || user.IsLoginless)
        {
            return Unauthorized(new { message = InvalidCredentials });
        }

        // Honour Identity lockout so repeated wrong passwords throttle brute force.
        if (await _userManager.IsLockedOutAsync(user))
        {
            return Unauthorized(new { message = "Konto vorübergehend gesperrt. Bitte später erneut versuchen." });
        }

        if (!await _userManager.CheckPasswordAsync(user, request.Password))
        {
            // Count the failure (locks the account after the configured threshold).
            await _userManager.AccessFailedAsync(user);
            return Unauthorized(new { message = InvalidCredentials });
        }

        // Successful login clears the failure counter.
        if (await _userManager.GetAccessFailedCountAsync(user) > 0)
        {
            await _userManager.ResetAccessFailedCountAsync(user);
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
