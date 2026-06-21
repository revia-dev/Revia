using System.Net.Http.Json;
using Revia.Client.Offline;
using Revia.Shared.Access;

namespace Revia.Client.Features.Access;

/// <summary>
/// Admin-only management of per-user module grants. Online-only — this is an admin
/// task and there is no value in queuing it offline.
/// </summary>
public sealed class AdminPermissionsClientService(
    IHttpClientFactory httpFactory,
    ConnectivityService connectivity)
{
    private readonly HttpClient _http = httpFactory.CreateClient("api");
    private readonly ConnectivityService _connectivity = connectivity;

    public bool IsOnline => _connectivity.IsOnline;

    public async Task<List<AdminUserDto>> GetUsersAsync()
    {
        if (!_connectivity.IsOnline) { return new(); }
        try { return await _http.GetFromJsonAsync<List<AdminUserDto>>("api/admin/permissions/users") ?? new(); }
        catch { return new(); }
    }

    public async Task<UserGrantsDto?> GetGrantsAsync(string userId)
    {
        if (!_connectivity.IsOnline) { return null; }
        try { return await _http.GetFromJsonAsync<UserGrantsDto>($"api/admin/permissions/users/{userId}"); }
        catch { return null; }
    }

    public async Task<bool> SetGrantAsync(string userId, string module, bool canRead, bool canWrite)
    {
        if (!_connectivity.IsOnline) { return false; }
        try
        {
            var r = await _http.PutAsJsonAsync($"api/admin/permissions/users/{userId}",
                new SetGrantRequest { Module = module, CanRead = canRead, CanWrite = canWrite });
            return r.IsSuccessStatusCode;
        }
        catch { return false; }
    }
}
