using System.Text.Json;
using Microsoft.JSInterop;
using PrivateLibrary.Shared.Offline;

namespace PrivateLibrary.Client.Offline;

/// <summary>
/// Thin C# wrapper over the window.reviaOffline IndexedDB helper. Provides the
/// read-cache and outbox primitives the repositories and sync engine build on.
/// </summary>
public sealed class OfflineStore(IJSRuntime js)
{
    private readonly IJSRuntime _js = js;

    // camelCase so the JSON keys match what the JS layer (IndexedDB key paths,
    // the API DTOs) and the browser expect.
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    // ---- read cache ----

    public async Task<T?> CacheGetAsync<T>(string key)
    {
        var json = await _js.InvokeAsync<string?>("reviaOffline.cacheGet", key);
        return string.IsNullOrEmpty(json) ? default : JsonSerializer.Deserialize<T>(json, Json);
    }

    public async Task CacheSetAsync<T>(string key, T value)
        => await _js.InvokeVoidAsync("reviaOffline.cacheSet", key, JsonSerializer.Serialize(value, Json));

    public async Task CacheRemoveAsync(string key)
        => await _js.InvokeVoidAsync("reviaOffline.cacheRemove", key);

    // ---- outbox ----

    public async Task OutboxAddAsync(OutboxMutation mutation)
        => await _js.InvokeVoidAsync("reviaOffline.outboxAdd", JsonSerializer.Serialize(mutation, Json));

    /// <summary>Persists changes to an existing mutation (same id ⇒ IndexedDB put
    /// overwrites). Used to record attempts/backoff/poison state.</summary>
    public async Task OutboxUpdateAsync(OutboxMutation mutation)
        => await _js.InvokeVoidAsync("reviaOffline.outboxAdd", JsonSerializer.Serialize(mutation, Json));

    public async Task<List<OutboxMutation>> OutboxAllAsync()
    {
        var json = await _js.InvokeAsync<string>("reviaOffline.outboxAll");
        return JsonSerializer.Deserialize<List<OutboxMutation>>(json, Json) ?? new();
    }

    public async Task OutboxRemoveAsync(string id)
        => await _js.InvokeVoidAsync("reviaOffline.outboxRemove", id);

    public async Task<int> OutboxCountAsync()
        => await _js.InvokeAsync<int>("reviaOffline.outboxCount");

    // ---- connectivity ----

    public async Task<bool> IsOnlineAsync()
        => await _js.InvokeAsync<bool>("reviaOffline.isOnline");

    public async Task RegisterConnectivityAsync(DotNetObjectReference<ConnectivityService> dotNetRef)
        => await _js.InvokeVoidAsync("reviaOffline.registerConnectivity", dotNetRef);
}
