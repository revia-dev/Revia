using System.Net.Http.Json;
using Revia.Client.Offline;
using Revia.Shared.Dashboard;

namespace Revia.Client.Features.Dashboard;

/// <summary>Read-only dashboard data: counts + KPIs, cached for offline display.</summary>
public sealed class DashboardClientService(
    IHttpClientFactory httpFactory,
    OfflineStore store,
    ConnectivityService connectivity)
{
    private readonly HttpClient _http = httpFactory.CreateClient("api");
    private readonly OfflineStore _store = store;
    private readonly ConnectivityService _connectivity = connectivity;
    private const string CacheKey = "dashboard";

    public async Task<DashboardDto> GetAsync()
    {
        if (_connectivity.IsOnline)
        {
            try
            {
                var fresh = await _http.GetFromJsonAsync<DashboardDto>("api/dashboard");
                if (fresh is not null)
                {
                    await _store.CacheSetAsync(CacheKey, fresh);
                    return fresh;
                }
            }
            catch { }
        }
        return await _store.CacheGetAsync<DashboardDto>(CacheKey) ?? new();
    }
}
