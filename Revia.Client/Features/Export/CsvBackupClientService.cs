using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.JSInterop;
using Revia.Client.Offline;
using Revia.Shared.Export;

namespace Revia.Client.Features.Export;

/// <summary>
/// CSV export + import for a module (online-only). Export fetches the CSV through the
/// authenticated client and triggers a browser download; import posts a chosen file
/// and returns the per-row result. Module key matches the API route segment
/// (e.g. "bmi", "movies", "lending", "matches").
/// </summary>
public sealed class CsvBackupClientService(
    IHttpClientFactory httpFactory,
    ConnectivityService connectivity,
    IJSRuntime js)
{
    private readonly HttpClient _http = httpFactory.CreateClient("api");
    private readonly ConnectivityService _connectivity = connectivity;
    private readonly IJSRuntime _js = js;

    public bool IsOnline => _connectivity.IsOnline;

    /// <summary>Downloads the module's CSV export (authenticated fetch → blob download).</summary>
    public async Task<bool> ExportAsync(string module, string fileName)
    {
        if (!_connectivity.IsOnline) return false;
        try
        {
            using var response = await _http.GetAsync($"api/csv/{module}/export");
            if (!response.IsSuccessStatusCode) return false;
            var bytes = await response.Content.ReadAsByteArrayAsync();
            await _js.InvokeVoidAsync("reviaDownload", fileName, Convert.ToBase64String(bytes), "text/csv");
            return true;
        }
        catch { return false; }
    }

    /// <summary>Fetches the module's CSV export as text (for File System Access /
    /// OPFS backup), or null when offline / on error.</summary>
    public async Task<string?> GetCsvTextAsync(string module)
    {
        if (!_connectivity.IsOnline) return null;
        try
        {
            using var response = await _http.GetAsync($"api/csv/{module}/export");
            return response.IsSuccessStatusCode
                ? await response.Content.ReadAsStringAsync()
                : null;
        }
        catch { return null; }
    }

    /// <summary>Uploads a CSV file to the module's import endpoint.</summary>
    public async Task<CsvImportResult?> ImportAsync(string module, Stream content, string fileName)
    {
        if (!_connectivity.IsOnline) return null;
        try
        {
            using var form = new MultipartFormDataContent();
            var fileContent = new StreamContent(content);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
            form.Add(fileContent, "file", fileName);

            var response = await _http.PostAsync($"api/csv/{module}/import", form);
            if (!response.IsSuccessStatusCode) { return null; }
            return await response.Content.ReadFromJsonAsync<CsvImportResult>();
        }
        catch { return null; }
    }
}
