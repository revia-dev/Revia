using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Revia.Data;

namespace Revia.Components.Account;

[ApiController]
public sealed class AdminAuthController : ControllerBase
{
    // Login must be reachable while unauthenticated; otherwise the global
    // RequireAuthenticatedUser fallback policy blocks the sign-in POST with 401.
    [AllowAnonymous]
    [HttpPost("/account/do-login")]
    [ValidateAntiForgeryToken]
    public async Task<IResult> Login(
        [FromServices] SignInManager<ApplicationUser> signInManager,
        [FromForm] string userName,
        [FromForm] string password,
        [FromForm] bool rememberMe,
        [FromForm] string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(password))
            return Results.Redirect($"/account/login?error=1&returnUrl={Uri.EscapeDataString(returnUrl ?? "/")}");

        await HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);

        var result = await signInManager.PasswordSignInAsync(
            userName.Trim(),
            password,
            isPersistent: rememberMe,
            lockoutOnFailure: true);

        if (result.Succeeded)
        {
            var target = Uri.UnescapeDataString(returnUrl ?? "/");
            if (!Url.IsLocalUrl(target))
                target = "/";
            return Results.Redirect(target);
        }

        return Results.Redirect($"/account/login?error=1&returnUrl={Uri.EscapeDataString(returnUrl ?? "/")}");
    }

    [HttpPost("/account/logout")]
    [ValidateAntiForgeryToken]
    public async Task<IResult> Logout(
        [FromServices] SignInManager<ApplicationUser> signInManager,
        [FromForm] string? returnUrl)
    {
        await signInManager.SignOutAsync();
        return Results.Redirect(returnUrl ?? "/");
    }
}
