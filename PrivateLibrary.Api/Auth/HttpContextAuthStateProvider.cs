using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace PrivateLibrary.Api.Auth;

/// <summary>
/// Bridges the JWT-authenticated <see cref="HttpContext.User"/> into the
/// <see cref="AuthenticationStateProvider"/> abstraction that the reused services
/// (via CurrentUserContext) depend on. This lets the existing household-scoped
/// services run unchanged inside the API, driven by the request's claims.
/// </summary>
public sealed class HttpContextAuthStateProvider(IHttpContextAccessor accessor) : AuthenticationStateProvider
{
    private readonly IHttpContextAccessor _accessor = accessor;

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var user = _accessor.HttpContext?.User ?? new ClaimsPrincipal(new ClaimsIdentity());
        return Task.FromResult(new AuthenticationState(user));
    }
}
