using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace PrivateLibrary.Data;

/// <summary>Custom claim types used across the app.</summary>
public static class HouseholdClaims
{
    public const string HouseholdId = "household_id";
}

/// <summary>
/// Embeds the user's <see cref="ApplicationUser.HouseholdId"/> as a claim at sign-in
/// so that the current household can be resolved synchronously from the authenticated
/// principal inside interactive Blazor circuits (where HttpContext is not available).
/// </summary>
public sealed class HouseholdClaimsPrincipalFactory(
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole> roleManager,
    IOptions<IdentityOptions> options)
    : UserClaimsPrincipalFactory<ApplicationUser, IdentityRole>(userManager, roleManager, options)
{
    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);
        if (user.HouseholdId is { } householdId)
        {
            identity.AddClaim(new Claim(HouseholdClaims.HouseholdId, householdId.ToString()));
        }
        return identity;
    }
}
