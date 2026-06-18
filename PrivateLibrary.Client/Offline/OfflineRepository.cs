using System.Net.Http.Json;
using System.Text.Json;
using PrivateLibrary.Shared.Offline;

namespace PrivateLibrary.Client.Offline;

/// <summary>
/// Generic offline-capable repository for one collection. Reads come from the API
/// when online (and are cached), or from the IndexedDB cache when offline. Writes
/// are applied optimistically to the local cache and queued in the outbox; if
/// online, the queue is flushed immediately. Conflicts resolve server-wins: a
/// refresh after sync replaces local state with the server's.
/// </summary>
public sealed class OfflineRepository<TItem>(
    IHttpClientFactory httpFactory,
    OfflineStore store,
    ConnectivityService connectivity,
    IOfflineCollection<TItem> collection)
{
    private readonly HttpClient _http = httpFactory.CreateClient("api");
    private readonly OfflineStore _store = store;
    private readonly ConnectivityService _connectivity = connectivity;
    private readonly IOfflineCollection<TItem> _c = collection;

    private string CacheKey => $"col:{_c.Name}";

    /// <summary>Returns the list. Online: fetch from API and refresh the cache.
    /// Offline (or on network error): return the last cached list.</summary>
    public async Task<List<TItem>> GetAllAsync()
    {
        if (_connectivity.IsOnline)
        {
            try
            {
                var fresh = await _http.GetFromJsonAsync<List<TItem>>(_c.ListPath);
                if (fresh is not null)
                {
                    await _store.CacheSetAsync(CacheKey, fresh);
                    return fresh;
                }
            }
            catch
            {
                // fall through to cache
            }
        }

        return await _store.CacheGetAsync<List<TItem>>(CacheKey) ?? new();
    }

    /// <summary>Reads the current cached list (no network), e.g. for optimistic edits.</summary>
    public async Task<List<TItem>> GetCachedAsync()
        => await _store.CacheGetAsync<List<TItem>>(CacheKey) ?? new();

    public async Task CreateAsync<TPayload>(TPayload payload, TItem optimisticItem)
    {
        await ApplyOptimisticAsync(list => list.Add(optimisticItem));
        await EnqueueAsync(new OutboxMutation
        {
            Collection = _c.Name,
            Kind = MutationKind.Create,
            PayloadJson = JsonSerializer.Serialize(payload)
        });
    }

    public async Task UpdateAsync<TPayload>(string id, TPayload payload, TItem optimisticItem)
    {
        await ApplyOptimisticAsync(list =>
        {
            var idx = list.FindIndex(x => _c.GetId(x) == id);
            if (idx >= 0) list[idx] = optimisticItem; else list.Add(optimisticItem);
        });
        await EnqueueAsync(new OutboxMutation
        {
            Collection = _c.Name,
            Kind = MutationKind.Update,
            EntityId = id,
            PayloadJson = JsonSerializer.Serialize(payload)
        });
    }

    public async Task DeleteAsync(string id)
    {
        await ApplyOptimisticAsync(list => list.RemoveAll(x => _c.GetId(x) == id));
        await EnqueueAsync(new OutboxMutation
        {
            Collection = _c.Name,
            Kind = MutationKind.Delete,
            EntityId = id
        });
    }

    private async Task ApplyOptimisticAsync(Action<List<TItem>> mutate)
    {
        var list = await GetCachedAsync();
        mutate(list);
        await _store.CacheSetAsync(CacheKey, list);
    }

    private async Task EnqueueAsync(OutboxMutation mutation)
    {
        await _store.OutboxAddAsync(mutation);
        // Best-effort immediate flush when online; otherwise it waits for the
        // connectivity-triggered sync.
        if (_connectivity.IsOnline)
        {
            await TryReplayAsync(mutation);
        }
    }

    /// <summary>Replays a single mutation against the API. Removes it from the
    /// outbox on success or on a non-retryable client error (server-wins).</summary>
    internal async Task<bool> TryReplayAsync(OutboxMutation m)
    {
        try
        {
            HttpResponseMessage response = m.Kind switch
            {
                MutationKind.Create => await _http.PostAsync(_c.ListPath, JsonContent(m.PayloadJson)),
                MutationKind.Update => await _http.PutAsync(_c.ItemPath(m.EntityId!), JsonContent(m.PayloadJson)),
                MutationKind.Delete => await _http.DeleteAsync(_c.ItemPath(m.EntityId!)),
                _ => throw new InvalidOperationException()
            };

            if (response.IsSuccessStatusCode ||
                (int)response.StatusCode is >= 400 and < 500)
            {
                // Success, or a client error we won't keep retrying (e.g. the entity
                // was already deleted server-side). Either way: server wins, drop it.
                await _store.OutboxRemoveAsync(m.Id);
                return true;
            }
        }
        catch
        {
            // network/server error — keep it queued for the next sync
        }
        return false;
    }

    private static StringContent JsonContent(string? json)
        => new(json ?? "{}", System.Text.Encoding.UTF8, "application/json");
}
