using Microsoft.JSInterop;

namespace Revia.Client.Features.Backup;

/// <summary>
/// Global JSON backup (admin-only, online): downloads the whole-database backup and
/// uploads one to restore it. The file format is shared with the native app, so the
/// two databases can be kept consistent by exchanging it.
/// </summary>
public sealed class BackupClientService(IHttpClientFactory httpFactory, IJSRuntime js)
{
    private readonly HttpClient _http = httpFactory.CreateClient("api");
    private readonly IJSRuntime _js = js;

    /// <summary>Downloads the backup JSON (authenticated fetch → blob download).</summary>
    public async Task<bool> ExportAsync()
    {
        try
        {
            using var response = await _http.GetAsync("api/backup");
            if (!response.IsSuccessStatusCode) return false;
            var bytes = await response.Content.ReadAsByteArrayAsync();
            await _js.InvokeVoidAsync("reviaDownload", "revia-backup.json",
                Convert.ToBase64String(bytes), "application/json");
            return true;
        }
        catch { return false; }
    }

    /// <summary>Uploads a backup JSON to restore it; returns the comma-joined names of
    /// the overwritten tables, or null on failure.</summary>
    public async Task<string?> ImportAsync(Stream content)
    {
        try
        {
            using var body = new StreamContent(content);
            body.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
            using var response = await _http.PostAsync("api/backup", body);
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadAsStringAsync();
        }
        catch { return null; }
    }
}
