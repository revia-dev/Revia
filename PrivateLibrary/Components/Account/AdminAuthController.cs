using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PrivateLibrary.Data;

namespace PrivateLibrary.Components.Account;

[ApiController]
public sealed class AdminAuthController : ControllerBase
{
    [HttpPost("/account/dev-admin-login")]
    [ValidateAntiForgeryToken]
    public async Task<IResult> Login(
        [FromServices] SignInManager<ApplicationUser> signInManager,
        [FromServices] UserManager<ApplicationUser> userManager,
        [FromForm] string? returnUrl)
    {
        var user = await userManager.FindByNameAsync(IdentitySeeder.DefaultAdminUserName);
        if (user is null)
        {
            return Results.Redirect(returnUrl ?? "/");
        }

        await HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
        var signInResult = await signInManager.PasswordSignInAsync(
            IdentitySeeder.DefaultAdminUserName,
            IdentitySeeder.DefaultAdminPassword,
            isPersistent: false,
            lockoutOnFailure: false);

        return Results.Redirect(signInResult.Succeeded ? (returnUrl ?? "/admin") : "/");
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
