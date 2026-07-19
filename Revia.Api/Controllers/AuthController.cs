using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Revia.Api.Auth;
using Revia.Data;
using Revia.Shared.Auth;

namespace Revia.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(
    UserManager<ApplicationUser> userManager,
    JwtTokenService tokens,
    ILogger<AuthController> logger) : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager = userManager;
    private readonly JwtTokenService _tokens = tokens;
    private readonly ILogger<AuthController> _logger = logger;

    private const string InvalidCredentials = "Benutzername oder Passwort ist falsch.";

    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var user = await _userManager.FindByNameAsync(request.UserName);
        if (user is null || user.IsLoginless)
        {
            _logger.LogWarning("Login failed for unknown/loginless user {User}", request.UserName);
            return Unauthorized(new { message = InvalidCredentials });
        }

        // Honour Identity lockout so repeated wrong passwords throttle brute force.
        if (await _userManager.IsLockedOutAsync(user))
        {
            _logger.LogWarning("Login blocked for locked-out user {UserId}", user.Id);
            return Unauthorized(new { message = "Konto vorübergehend gesperrt. Bitte später erneut versuchen." });
        }

        if (!await _userManager.CheckPasswordAsync(user, request.Password))
        {
            // Count the failure (locks the account after the configured threshold).
            await _userManager.AccessFailedAsync(user);
            _logger.LogWarning("Login failed (bad password) for user {UserId}", user.Id);
            return Unauthorized(new { message = InvalidCredentials });
        }

        // Successful login clears the failure counter.
        if (await _userManager.GetAccessFailedCountAsync(user) > 0)
        {
            await _userManager.ResetAccessFailedCountAsync(user);
        }

        _logger.LogInformation("User {UserId} logged in", user.Id);
        return await _tokens.IssueAsync(user, ct);
    }

    [AllowAnonymous]
    [EnableRateLimiting("auth")]
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

    /// <summary>Logs in as the demo user without credentials, so the app can be
    /// explored read-mostly. Only available when demo mode is enabled.</summary>
    [AllowAnonymous]
    [HttpPost("demo")]
    public async Task<ActionResult<AuthResponse>> Demo(
        [FromServices] IConfiguration configuration,
        [FromServices] RoleManager<IdentityRole> roleManager,
        [FromServices] AppDbContext db,
        CancellationToken ct)
    {
        if (!configuration.GetValue<bool>("Demo:Enabled"))
        {
            return NotFound();
        }

        // Every entry starts from the same isolated sample state. Changes made during
        // a demo session disappear the next time the demo is opened.
        var demo = await DemoSeeder.SeedAsync(_userManager, roleManager, db, reset: true, ct);

        return await _tokens.IssueAsync(demo, ct);
    }
}
