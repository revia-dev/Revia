using System.Net.Http.Json;
using PrivateLibrary.Client.Offline;
using PrivateLibrary.Shared.Persons;

namespace PrivateLibrary.Client.Features.Persons;

/// <summary>
/// People in the household + cross-household linking (share a person → the other
/// household imports them and the two households' lendings become shared). The list
/// and linked households are cached for offline display; preview/import are
/// online-only (they touch another household live).
/// </summary>
public sealed class PersonsClientService(
    IHttpClientFactory httpFactory,
    OfflineStore store,
    ConnectivityService connectivity)
{
    private readonly HttpClient _http = httpFactory.CreateClient("api");
    private readonly OfflineStore _store = store;
    private readonly ConnectivityService _connectivity = connectivity;

    private const string PeopleKey = "persons:cards";
    private const string LinkedKey = "persons:linked-households";

    public bool IsOnline => _connectivity.IsOnline;

    public async Task<List<PersonCardDto>> GetAllAsync()
    {
        if (_connectivity.IsOnline)
        {
            try
            {
                var fresh = await _http.GetFromJsonAsync<List<PersonCardDto>>("api/persons");
                if (fresh is not null) { await _store.CacheSetAsync(PeopleKey, fresh); return fresh; }
            }
            catch { }
        }
        return await _store.CacheGetAsync<List<PersonCardDto>>(PeopleKey) ?? new();
    }

    public async Task<List<LinkedHouseholdDto>> GetLinkedHouseholdsAsync()
    {
        if (_connectivity.IsOnline)
        {
            try
            {
                var fresh = await _http.GetFromJsonAsync<List<LinkedHouseholdDto>>("api/persons/linked-households");
                if (fresh is not null) { await _store.CacheSetAsync(LinkedKey, fresh); return fresh; }
            }
            catch { }
        }
        return await _store.CacheGetAsync<List<LinkedHouseholdDto>>(LinkedKey) ?? new();
    }

    public async Task<PersonLinkPreviewDto?> PreviewLinkAsync(string token)
    {
        if (!_connectivity.IsOnline) return null;
        try { return await _http.GetFromJsonAsync<PersonLinkPreviewDto>($"api/persons/link/{token}"); }
        catch { return null; }
    }

    public async Task<PersonLinkResultDto> ImportLinkAsync(string token)
    {
        if (!_connectivity.IsOnline)
        {
            return new PersonLinkResultDto { Success = false, Error = "Nur online möglich." };
        }
        try
        {
            var r = await _http.PostAsync($"api/persons/link/{token}", null);
            var result = await r.Content.ReadFromJsonAsync<PersonLinkResultDto>();
            return result ?? new PersonLinkResultDto { Success = r.IsSuccessStatusCode };
        }
        catch (Exception ex) { return new PersonLinkResultDto { Success = false, Error = ex.Message }; }
    }

    public async Task<PersonResultDto> SaveAsync(SavePersonRequest request)
    {
        if (!_connectivity.IsOnline) { return new PersonResultDto { Success = false, Error = "Nur online möglich." }; }
        try
        {
            var response = string.IsNullOrEmpty(request.Id)
                ? await _http.PostAsJsonAsync("api/persons", request)
                : await _http.PutAsJsonAsync($"api/persons/{request.Id}", request);
            return await response.Content.ReadFromJsonAsync<PersonResultDto>()
                ?? new PersonResultDto { Success = response.IsSuccessStatusCode };
        }
        catch (Exception ex) { return new PersonResultDto { Success = false, Error = ex.Message }; }
    }

    public async Task<bool> DeleteAsync(string id)
    {
        if (!_connectivity.IsOnline) return false;
        try { return (await _http.DeleteAsync($"api/persons/{id}")).IsSuccessStatusCode; }
        catch { return false; }
    }

    /// <summary>Admin-only: grants a loginless person a login.</summary>
    public async Task<PersonResultDto> AddLoginAsync(string id, AddLoginRequest request)
    {
        if (!_connectivity.IsOnline) { return new PersonResultDto { Success = false, Error = "Nur online möglich." }; }
        try
        {
            var response = await _http.PostAsJsonAsync($"api/persons/{id}/login", request);
            return await response.Content.ReadFromJsonAsync<PersonResultDto>()
                ?? new PersonResultDto { Success = response.IsSuccessStatusCode };
        }
        catch (Exception ex) { return new PersonResultDto { Success = false, Error = ex.Message }; }
    }
}
