using Microsoft.JSInterop;
using Revia.Client.Offline;

namespace Revia.Client.Features.Pwa;

/// <summary>Result of saving a file via the File System Access API (with download
/// fallback), so the UI can give accurate feedback.</summary>
public enum SaveFileOutcome { Saved, Downloaded, Cancelled, Failed }

/// <summary>How much offline data is stored, and whether the OS promised to keep it.</summary>
public readonly record struct StorageEstimate(long Usage, long Quota, bool Persisted);

/// <summary>
/// Thin .NET wrapper over the PWA capabilities in <c>js/pwa.js</c>: persistent
/// storage, the Badging API, File System Access (+ OPFS backup). Also keeps the
/// installed-app icon badge in sync with the number of pending (un-synced)
/// mutations, so the user can see at a glance that something still needs to upload.
/// Everything degrades gracefully when an API is unavailable.
/// </summary>
public sealed class PwaInterop : IDisposable
{
    private readonly IJSRuntime _js;
    private readonly SyncEngine _sync;
    private bool _wired;

    public PwaInterop(IJSRuntime js, SyncEngine sync)
    {
        _js = js;
        _sync = sync;
    }

    /// <summary>Requests persistent storage and starts mirroring the pending-sync
    /// count onto the app badge. Safe to call once at startup.</summary>
    public async Task InitializeAsync()
    {
        if (_wired) { return; }
        _wired = true;

        await PersistStorageAsync();

        _sync.Queued += () => _ = UpdateBadgeAsync();
        _sync.Synced += result => _ = UpdateBadgeAsync();
        await UpdateBadgeAsync();
    }

    public async Task<bool> PersistStorageAsync()
    {
        try { return await _js.InvokeAsync<bool>("reviaPersistStorage"); }
        catch { return false; }
    }

    public async Task<StorageEstimate> GetStorageEstimateAsync()
    {
        try
        {
            var raw = await _js.InvokeAsync<StorageEstimateDto>("reviaStorageEstimate");
            return new StorageEstimate(raw.Usage, raw.Quota, raw.Persisted);
        }
        catch { return default; }
    }

    public async Task UpdateBadgeAsync()
    {
        try
        {
            var pending = await _sync.PendingCountAsync();
            await _js.InvokeVoidAsync("reviaSetBadge", pending);
        }
        catch { /* badging unsupported — ignore */ }
    }

    /// <summary>Saves text to a user-chosen file; falls back to a download.</summary>
    public async Task<SaveFileOutcome> SaveTextFileAsync(string suggestedName, string text, string mimeType = "text/csv")
    {
        try
        {
            var result = await _js.InvokeAsync<string>("reviaSaveTextFile", suggestedName, text, mimeType);
            return result switch
            {
                "saved" => SaveFileOutcome.Saved,
                "downloaded" => SaveFileOutcome.Downloaded,
                "cancelled" => SaveFileOutcome.Cancelled,
                _ => SaveFileOutcome.Failed,
            };
        }
        catch { return SaveFileOutcome.Failed; }
    }

    /// <summary>Opens a text file the user picks, or null if cancelled/unsupported.</summary>
    public async Task<string?> OpenTextFileAsync()
    {
        try { return await _js.InvokeAsync<string?>("reviaOpenTextFile"); }
        catch { return null; }
    }

    /// <summary>Keeps a rolling local backup in the Origin Private File System.</summary>
    public async Task<bool> WriteOpfsBackupAsync(string name, string text)
    {
        try { return await _js.InvokeAsync<bool>("reviaOpfsWrite", name, text); }
        catch { return false; }
    }

    public async Task<string?> ReadOpfsBackupAsync(string name)
    {
        try { return await _js.InvokeAsync<string?>("reviaOpfsRead", name); }
        catch { return null; }
    }

    public void Dispose() { /* event handlers live for the app's lifetime */ }

    private sealed record StorageEstimateDto(long Usage, long Quota, bool Persisted);
}
