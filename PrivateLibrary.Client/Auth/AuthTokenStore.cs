using Microsoft.JSInterop;
using PrivateLibrary.Shared.Auth;
using System.Text.Json;

namespace PrivateLibrary.Client.Auth;

/// <summary>
/// Persists the current auth session (tokens + profile snapshot) in localStorage so
/// the user stays signed in across reloads and while offline. The profile snapshot
/// lets the app render an authenticated UI offline without contacting the server.
///
/// Backed by window.reviaTokenStore (a plain global script, not an ES module) to
/// avoid the fingerprinted import-map indirection for hand-authored JS.
/// </summary>
public sealed class AuthTokenStore(IJSRuntime js)
{
    private const string Key = "revia.auth";
    private readonly IJSRuntime _js = js;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<AuthResponse?> LoadAsync()
    {
        var raw = await _js.InvokeAsync<string?>("reviaTokenStore.get", Key);
        if (string.IsNullOrEmpty(raw))
        {
            return null;
        }
        try
        {
            return JsonSerializer.Deserialize<AuthResponse>(raw, Json);
        }
        catch
        {
            return null;
        }
    }

    public async Task SaveAsync(AuthResponse auth)
        => await _js.InvokeVoidAsync("reviaTokenStore.set", Key, JsonSerializer.Serialize(auth, Json));

    public async Task ClearAsync()
        => await _js.InvokeVoidAsync("reviaTokenStore.remove", Key);
}
