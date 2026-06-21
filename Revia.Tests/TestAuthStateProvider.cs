using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Revia.Data;

namespace Revia.Tests;

/// <summary>
/// Minimal <see cref="AuthenticationStateProvider"/> for unit tests. Lets a test
/// construct a <see cref="Revia.Services.CurrentUserContext"/> with a
/// fixed user/household without any web host.
/// </summary>
public sealed class TestAuthStateProvider : AuthenticationStateProvider
{
    private readonly AuthenticationState _state;

    public TestAuthStateProvider(string userId = "test-user", int? householdId = 1)
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, userId) };
        if (householdId.HasValue)
        {
            claims.Add(new Claim(HouseholdClaims.HouseholdId, householdId.Value.ToString()));
        }

        var identity = new ClaimsIdentity(claims, authenticationType: "Test");
        _state = new AuthenticationState(new ClaimsPrincipal(identity));
    }

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
        => Task.FromResult(_state);
}
