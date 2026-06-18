using System.Net.Http.Json;
using Microsoft.AspNetCore.Components.Authorization;
using PrivateLibrary.Client.Offline;
using PrivateLibrary.Shared.Access;

namespace PrivateLibrary.Client.Features.Access;

/// <summary>
/// The current user's effective module access + the module catalog, cached so the
/// nav, dashboard tiles and write buttons can gate themselves (also offline). This
/// is UX only — the API enforces the same rules server-side (ModuleAccessAttribute).
///
/// Resolved once per app load and shared; refreshed on demand after an admin change.
/// </summary>
public sealed class AccessClientService
{
    private readonly HttpClient _http;
    private readonly OfflineStore _store;
    private readonly ConnectivityService _connectivity;

    private const string AccessKey = "access:me";
    private const string ModulesKey = "access:modules";

    private MyAccessDto? _access;
    private Dictionary<string, ModuleAccessDto> _byModule = new(StringComparer.Ordinal);

    public AccessClientService(
        IHttpClientFactory httpFactory,
        OfflineStore store,
        ConnectivityService connectivity,
        AuthenticationStateProvider authState)
    {
        _http = httpFactory.CreateClient("api");
        _store = store;
        _connectivity = connectivity;
        // The effective access belongs to the signed-in user; drop it whenever the
        // auth state changes (login/logout) so the next read re-resolves for the new
        // user instead of serving the previous user's cached grants.
        authState.AuthenticationStateChanged += _ => Invalidate();
    }

    /// <summary>Forces the next access read to re-resolve (e.g. after login/logout
    /// or an admin grant change).</summary>
    public void Invalidate()
    {
        _access = null;
        _byModule = new(StringComparer.Ordinal);
    }

    public bool IsAdmin => _access?.IsAdmin ?? false;

    /// <summary>Loads (and caches) the effective access. Call once before gating.</summary>
    public async Task<MyAccessDto> GetMyAccessAsync(bool forceRefresh = false)
    {
        if (_access is not null && !forceRefresh)
        {
            return _access;
        }

        if (_connectivity.IsOnline)
        {
            try
            {
                var fresh = await _http.GetFromJsonAsync<MyAccessDto>("api/access/me");
                if (fresh is not null)
                {
                    await _store.CacheSetAsync(AccessKey, fresh);
                    Apply(fresh);
                    return fresh;
                }
            }
            catch { }
        }

        var cached = await _store.CacheGetAsync<MyAccessDto>(AccessKey);
        // No cache yet (first run offline) => assume full access; the API still guards.
        Apply(cached ?? new MyAccessDto { IsAdmin = false, Modules = new() });
        return _access!;
    }

    private void Apply(MyAccessDto access)
    {
        _access = access;
        _byModule = access.Modules.ToDictionary(m => m.Module, StringComparer.Ordinal);
    }

    public bool CanRead(string moduleKey)
        => IsAdmin || !_byModule.TryGetValue(moduleKey, out var a) || a.CanRead;

    public bool CanWrite(string moduleKey)
        => IsAdmin || !_byModule.TryGetValue(moduleKey, out var a) || a.CanWrite;

    public async Task<List<ModuleInfoDto>> GetModulesAsync()
    {
        if (_connectivity.IsOnline)
        {
            try
            {
                var fresh = await _http.GetFromJsonAsync<List<ModuleInfoDto>>("api/access/modules");
                if (fresh is not null) { await _store.CacheSetAsync(ModulesKey, fresh); return fresh; }
            }
            catch { }
        }
        return await _store.CacheGetAsync<List<ModuleInfoDto>>(ModulesKey) ?? new();
    }
}
