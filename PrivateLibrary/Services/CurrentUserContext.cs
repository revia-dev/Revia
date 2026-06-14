using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using PrivateLibrary.Data;

namespace PrivateLibrary.Services;

/// <summary>
/// Resolves information about the currently signed-in user — most importantly the
/// household (tenant) they belong to. Backed by the <see cref="AuthenticationStateProvider"/>
/// so it works both during SSR and inside an interactive circuit.
/// </summary>
public sealed class CurrentUserContext(AuthenticationStateProvider authStateProvider)
{
    private readonly AuthenticationStateProvider _authStateProvider = authStateProvider;

    public async Task<ClaimsPrincipal> GetUserAsync()
    {
        var state = await _authStateProvider.GetAuthenticationStateAsync();
        return state.User;
    }

    /// <summary>The current user's household id, or null if not signed in / not assigned.</summary>
    public async Task<int?> GetHouseholdIdAsync()
    {
        var user = await GetUserAsync();
        var raw = user.FindFirstValue(HouseholdClaims.HouseholdId);
        return int.TryParse(raw, out var id) ? id : null;
    }

    /// <summary>The current user's household id, throwing if none is available.</summary>
    public async Task<int> RequireHouseholdIdAsync()
    {
        return await GetHouseholdIdAsync()
            ?? throw new InvalidOperationException("Kein Haushalt für den aktuellen Benutzer ermittelt.");
    }

    public async Task<string?> GetUserIdAsync()
    {
        var user = await GetUserAsync();
        return user.FindFirstValue(ClaimTypes.NameIdentifier);
    }
}
