using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using PrivateLibrary.Shared.Auth;

namespace PrivateLibrary.Client.Auth;

/// <summary>
/// Token-based authentication for the WASM client. Owns the current session,
/// exposes the auth state to the component tree, and handles login / logout /
/// silent refresh. Designed to keep working offline: the last known session is
/// restored from <see cref="AuthTokenStore"/> so the UI renders authenticated
/// without a server round-trip; the token is refreshed lazily when online.
/// </summary>
public sealed class AuthService(
    IHttpClientFactory httpFactory,
    AuthTokenStore store) : AuthenticationStateProvider
{
    // A bare client (no auth handler) for the auth endpoints themselves.
    private readonly HttpClient _http = httpFactory.CreateClient("auth");
    private readonly AuthTokenStore _store = store;

    private AuthResponse? _session;
    private AuthenticationState _anonymous = new(new ClaimsPrincipal(new ClaimsIdentity()));

    public AuthResponse? Session => _session;

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        _session ??= await _store.LoadAsync();
        if (_session is null)
        {
            return _anonymous;
        }

        return new AuthenticationState(BuildPrincipal(_session));
    }

    public async Task<(bool ok, string? error)> LoginAsync(string userName, string password)
    {
        try
        {
            var response = await _http.PostAsJsonAsync("api/auth/login",
                new LoginRequest { UserName = userName, Password = password });

            if (!response.IsSuccessStatusCode)
            {
                return (false, "Benutzername oder Passwort ist falsch.");
            }

            var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
            if (auth is null)
            {
                return (false, "Anmeldung fehlgeschlagen.");
            }

            await SetSessionAsync(auth);
            return (true, null);
        }
        catch
        {
            return (false, "Server nicht erreichbar.");
        }
    }

    public async Task LogoutAsync()
    {
        if (_session is not null)
        {
            try
            {
                await _http.PostAsJsonAsync("api/auth/logout",
                    new RefreshRequest { RefreshToken = _session.RefreshToken });
            }
            catch
            {
                // Offline logout still clears the local session.
            }
        }

        _session = null;
        await _store.ClearAsync();
        NotifyAuthenticationStateChanged(Task.FromResult(_anonymous));
    }

    /// <summary>Returns a valid access token, refreshing it if it is about to expire.
    /// Used by the auth message handler. Returns null when offline/unauthenticated.</summary>
    public async Task<string?> GetAccessTokenAsync()
    {
        _session ??= await _store.LoadAsync();
        if (_session is null)
        {
            return null;
        }

        if (_session.AccessTokenExpiresAt > DateTimeOffset.UtcNow.AddMinutes(1))
        {
            return _session.AccessToken;
        }

        // Token (nearly) expired — try a silent refresh. If offline, keep the old
        // token; the request may fail and the UI falls back to cached data.
        try
        {
            var response = await _http.PostAsJsonAsync("api/auth/refresh",
                new RefreshRequest { RefreshToken = _session.RefreshToken });
            if (response.IsSuccessStatusCode)
            {
                var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
                if (auth is not null)
                {
                    await SetSessionAsync(auth);
                    return auth.AccessToken;
                }
            }
        }
        catch
        {
            // offline — fall through
        }

        return _session.AccessToken;
    }

    private async Task SetSessionAsync(AuthResponse auth)
    {
        _session = auth;
        await _store.SaveAsync(auth);
        NotifyAuthenticationStateChanged(
            Task.FromResult(new AuthenticationState(BuildPrincipal(auth))));
    }

    private static ClaimsPrincipal BuildPrincipal(AuthResponse auth)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, auth.UserId),
            new(ClaimTypes.Name, auth.UserName),
            new("household_id", auth.HouseholdId?.ToString() ?? string.Empty),
        };
        claims.AddRange(auth.Roles.Select(r => new Claim(ClaimTypes.Role, r)));

        var identity = new ClaimsIdentity(claims, authenticationType: "jwt");
        return new ClaimsPrincipal(identity);
    }
}
