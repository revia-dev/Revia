using System.Net.Http.Json;
using PrivateLibrary.Client.Offline;
using PrivateLibrary.Shared.Analytics;

namespace PrivateLibrary.Client.Features.Analytics;

/// <summary>
/// User-configurable analytics widgets. Widgets + their evaluated results and the
/// metric catalog are cached for offline display. CRUD is online-first (the result
/// depends on live server-side evaluation); when offline, writes are reported as
/// unavailable to the caller and the cached widgets are shown read-only.
/// </summary>
public sealed class AnalyticsClientService(
    IHttpClientFactory httpFactory,
    OfflineStore store,
    ConnectivityService connectivity)
{
    private readonly HttpClient _http = httpFactory.CreateClient("api");
    private readonly OfflineStore _store = store;
    private readonly ConnectivityService _connectivity = connectivity;

    private const string WidgetsKey = "analytics:widgets";
    private const string MetricsKey = "analytics:metrics";

    public bool IsOnline => _connectivity.IsOnline;

    public async Task<List<AnalyticsWidgetDto>> GetWidgetsAsync()
    {
        if (_connectivity.IsOnline)
        {
            try
            {
                var fresh = await _http.GetFromJsonAsync<List<AnalyticsWidgetDto>>("api/analytics");
                if (fresh is not null) { await _store.CacheSetAsync(WidgetsKey, fresh); return fresh; }
            }
            catch { }
        }
        return await _store.CacheGetAsync<List<AnalyticsWidgetDto>>(WidgetsKey) ?? new();
    }

    public async Task<List<AnalyticsMetricDto>> GetMetricsAsync()
    {
        if (_connectivity.IsOnline)
        {
            try
            {
                var fresh = await _http.GetFromJsonAsync<List<AnalyticsMetricDto>>("api/analytics/metrics");
                if (fresh is not null) { await _store.CacheSetAsync(MetricsKey, fresh); return fresh; }
            }
            catch { }
        }
        return await _store.CacheGetAsync<List<AnalyticsMetricDto>>(MetricsKey) ?? new();
    }

    /// <summary>Creates/updates a widget. Online-only (server evaluates the result).
    /// Returns false when offline.</summary>
    public async Task<bool> SaveAsync(SaveAnalyticsWidgetRequest request)
    {
        if (!_connectivity.IsOnline) { return false; }
        try
        {
            var response = request.Id > 0
                ? await _http.PutAsJsonAsync($"api/analytics/{request.Id}", request)
                : await _http.PostAsJsonAsync("api/analytics", request);
            return response.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    public async Task<bool> DeleteAsync(int id)
    {
        if (!_connectivity.IsOnline) { return false; }
        try { return (await _http.DeleteAsync($"api/analytics/{id}")).IsSuccessStatusCode; }
        catch { return false; }
    }

    public async Task<bool> MoveAsync(int id, int delta)
    {
        if (!_connectivity.IsOnline) { return false; }
        try
        {
            var r = await _http.PutAsJsonAsync($"api/analytics/{id}/move", new MoveAnalyticsWidgetRequest { Delta = delta });
            return r.IsSuccessStatusCode;
        }
        catch { return false; }
    }
}
