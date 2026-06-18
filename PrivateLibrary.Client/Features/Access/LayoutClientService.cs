using System.Net.Http.Json;
using PrivateLibrary.Client.Offline;
using PrivateLibrary.Shared.Access;

namespace PrivateLibrary.Client.Features.Access;

/// <summary>
/// The current user's personal dashboard layout (module-tile + section order and
/// visibility). Cached for offline display; saves are online-first (personalization,
/// not core data — and the resolved layout depends on the server's known sections).
/// </summary>
public sealed class LayoutClientService(
    IHttpClientFactory httpFactory,
    OfflineStore store,
    ConnectivityService connectivity)
{
    private readonly HttpClient _http = httpFactory.CreateClient("api");
    private readonly OfflineStore _store = store;
    private readonly ConnectivityService _connectivity = connectivity;
    private const string CacheKey = "layout";

    public bool IsOnline => _connectivity.IsOnline;

    public async Task<LayoutDto> GetAsync()
    {
        if (_connectivity.IsOnline)
        {
            try
            {
                var fresh = await _http.GetFromJsonAsync<LayoutDto>("api/layout");
                if (fresh is not null) { await _store.CacheSetAsync(CacheKey, fresh); return fresh; }
            }
            catch { }
        }
        return await _store.CacheGetAsync<LayoutDto>(CacheKey) ?? new();
    }

    public async Task<bool> SaveAsync(LayoutDto layout)
    {
        if (!_connectivity.IsOnline) { return false; }
        try
        {
            var request = new SaveLayoutRequest { Tiles = layout.Tiles, Sections = layout.Sections };
            var r = await _http.PutAsJsonAsync("api/layout", request);
            if (r.IsSuccessStatusCode) { await _store.CacheSetAsync(CacheKey, layout); return true; }
            return false;
        }
        catch { return false; }
    }

    public async Task<bool> ResetAsync()
    {
        if (!_connectivity.IsOnline) { return false; }
        try
        {
            var r = await _http.PostAsync("api/layout/reset", null);
            if (r.IsSuccessStatusCode) { await _store.CacheRemoveAsync(CacheKey); return true; }
            return false;
        }
        catch { return false; }
    }
}
