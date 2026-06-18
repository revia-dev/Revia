using System.Net.Http.Json;
using Microsoft.JSInterop;
using PrivateLibrary.Client.Offline;
using PrivateLibrary.Shared.Games;

namespace PrivateLibrary.Client.Features.Games;

/// <summary>
/// A game's files: rules document + expansions (with their files). Online-only —
/// these are managed on the game editor while connected; there's no offline value
/// in queuing binary uploads. The downloaded files themselves can still be cached by
/// AuthImage/PhotoCache where they're images, but rules/expansion files are arbitrary
/// documents fetched on demand.
/// </summary>
public sealed class GameFilesClientService(
    IHttpClientFactory httpFactory,
    ConnectivityService connectivity,
    IJSRuntime js)
{
    private readonly HttpClient _http = httpFactory.CreateClient("api");
    private readonly ConnectivityService _connectivity = connectivity;
    private readonly IJSRuntime _js = js;

    public bool IsOnline => _connectivity.IsOnline;

    /// <summary>Fetches an authenticated file (rules/expansion) and triggers a browser
    /// download. Needed because a plain link can't send the bearer token.</summary>
    public async Task DownloadAsync(string apiPath, string fileName)
    {
        if (!_connectivity.IsOnline) return;
        try
        {
            using var response = await _http.GetAsync(apiPath);
            if (!response.IsSuccessStatusCode) return;
            var bytes = await response.Content.ReadAsByteArrayAsync();
            var contentType = response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";
            await _js.InvokeVoidAsync("reviaDownload", fileName, Convert.ToBase64String(bytes), contentType);
        }
        catch { }
    }

    public async Task<GameDetailDto?> GetDetailAsync(int gameId)
    {
        if (!_connectivity.IsOnline) return null;
        try { return await _http.GetFromJsonAsync<GameDetailDto>($"api/games/{gameId}/detail"); }
        catch { return null; }
    }

    public async Task<bool> SetRulesAsync(int gameId, UploadFileRequest request)
        => await PostAsync($"api/games/{gameId}/rules", request);

    public async Task<bool> AddExpansionAsync(int gameId, AddExpansionRequest request)
        => await PostAsync($"api/games/{gameId}/expansions", request);

    public async Task<bool> DeleteExpansionAsync(int expansionId)
        => await DeleteAsync($"api/games/expansions/{expansionId}");

    public async Task<bool> AddExpansionFileAsync(int expansionId, UploadFileRequest request)
        => await PostAsync($"api/games/expansions/{expansionId}/files", request);

    public async Task<bool> DeleteExpansionFileAsync(int fileId)
        => await DeleteAsync($"api/games/expansions/file/{fileId}");

    private async Task<bool> PostAsync(string url, object body)
    {
        if (!_connectivity.IsOnline) return false;
        try { return (await _http.PostAsJsonAsync(url, body)).IsSuccessStatusCode; }
        catch { return false; }
    }

    private async Task<bool> DeleteAsync(string url)
    {
        if (!_connectivity.IsOnline) return false;
        try { return (await _http.DeleteAsync(url)).IsSuccessStatusCode; }
        catch { return false; }
    }
}
