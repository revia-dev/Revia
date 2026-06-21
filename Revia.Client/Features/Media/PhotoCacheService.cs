using Revia.Client.Offline;

namespace Revia.Client.Features.Media;

/// <summary>
/// Loads authenticated photos (served under /api, which the service worker never
/// caches and a plain &lt;img&gt; can't send a bearer token to) and makes them work
/// offline. The photo is fetched once through the authenticated HttpClient, turned
/// into a self-contained data: URL, and stored in IndexedDB keyed by its path. On a
/// later load — including offline — the cached data: URL is returned directly.
/// </summary>
public sealed class PhotoCacheService(
    IHttpClientFactory httpFactory,
    OfflineStore store,
    ConnectivityService connectivity)
{
    private readonly HttpClient _http = httpFactory.CreateClient("api");
    private readonly OfflineStore _store = store;
    private readonly ConnectivityService _connectivity = connectivity;

    // Small in-memory layer so repeated renders in one session don't re-hit IndexedDB.
    private readonly Dictionary<string, string?> _memory = new(StringComparer.Ordinal);

    /// <summary>Returns a data: URL for the photo at <paramref name="apiPath"/>
    /// (e.g. "api/bmi/3/photo"), or null if it isn't available. Online it fetches and
    /// caches; offline it returns the cached copy if there is one.</summary>
    public async Task<string?> GetDataUrlAsync(string apiPath)
    {
        if (string.IsNullOrWhiteSpace(apiPath))
        {
            return null;
        }

        if (_memory.TryGetValue(apiPath, out var cachedMem))
        {
            return cachedMem;
        }

        var key = CacheKey(apiPath);

        if (_connectivity.IsOnline)
        {
            try
            {
                using var response = await _http.GetAsync(apiPath);
                if (response.IsSuccessStatusCode)
                {
                    var bytes = await response.Content.ReadAsByteArrayAsync();
                    var contentType = response.Content.Headers.ContentType?.MediaType ?? "image/jpeg";
                    var dataUrl = $"data:{contentType};base64,{Convert.ToBase64String(bytes)}";
                    await _store.CacheSetAsync(key, dataUrl);
                    _memory[apiPath] = dataUrl;
                    return dataUrl;
                }
            }
            catch { /* fall through to cache */ }
        }

        var fromCache = await _store.CacheGetAsync<string>(key);
        _memory[apiPath] = fromCache;
        return fromCache;
    }

    private static string CacheKey(string apiPath) => "photo:" + apiPath;
}
