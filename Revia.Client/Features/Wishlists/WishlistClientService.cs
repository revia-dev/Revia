using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Revia.Client.Offline;
using Revia.Shared.Offline;
using Revia.Shared.Wishlists;

namespace Revia.Client.Features.Wishlists;

/// <summary>
/// Offline-capable wishlist data access for the client. Reads come from the API
/// (cached in IndexedDB) when online, or from the cache when offline. Writes apply
/// optimistically to the cache and queue mutations in the outbox; the SyncEngine
/// replays them on reconnect (server-wins). Wishlists and their items are handled
/// here together because they use distinct endpoints.
/// </summary>
public sealed class WishlistClientService : OfflineClientService
{
    public const string Collection = "wishlists";
    private const string CacheKey = "col:wishlists";

    public WishlistClientService(
        IHttpClientFactory httpFactory,
        OfflineStore store,
        ConnectivityService connectivity,
        SyncEngine sync)
        : base(Collection, httpFactory, store, connectivity, sync) { }

    // ---- reads ----

    public async Task<List<WishlistDto>> GetAllAsync()
    {
        if (_connectivity.IsOnline)
        {
            try
            {
                var fresh = await _http.GetFromJsonAsync<List<WishlistDto>>("api/wishlists");
                if (fresh is not null)
                {
                    await _store.CacheSetAsync(CacheKey, fresh);
                    return fresh;
                }
            }
            catch { /* fall through to cache */ }
        }
        return await _store.CacheGetAsync<List<WishlistDto>>(CacheKey) ?? new();
    }

    private async Task<List<WishlistDto>> CachedAsync()
        => await _store.CacheGetAsync<List<WishlistDto>>(CacheKey) ?? new();

    // ---- writes (optimistic cache + outbox) ----

    public async Task CreateListAsync(string name)
    {
        var list = await CachedAsync();
        // Optimistic placeholder with a negative temp id until the server assigns one.
        list.Add(new WishlistDto { Id = TempId(), Name = name, CanEdit = true });
        await _store.CacheSetAsync(CacheKey, list);

        await EnqueueAsync(MutationKind.Create, null, new CreateWishlistRequest { Name = name });
    }

    public async Task DeleteListAsync(int id)
    {
        var list = await CachedAsync();
        list.RemoveAll(w => w.Id == id);
        await _store.CacheSetAsync(CacheKey, list);

        if (id > 0) // only real server entities need a delete call
        {
            await EnqueueAsync(MutationKind.Delete, id.ToString(), null);
        }
    }

    public async Task AddItemAsync(int wishlistId, string title, string? url, string? notes, decimal? price, byte[]? photoData = null)
    {
        var list = await CachedAsync();
        var w = list.FirstOrDefault(x => x.Id == wishlistId);
        w?.Items.Add(new WishlistItemDto { Id = TempId(), Title = title, Url = url, Notes = notes, Price = price, PhotoData = photoData });
        await _store.CacheSetAsync(CacheKey, list);

        await EnqueueAsync(MutationKind.Create, "items", new AddWishlistItemRequest
        {
            WishlistId = wishlistId,
            Title = title,
            Url = url,
            Notes = notes,
            Price = price,
            PhotoData = photoData
        });
    }

    public async Task SetItemFulfilledAsync(int wishlistId, int itemId, bool fulfilled)
    {
        var list = await CachedAsync();
        var item = list.FirstOrDefault(x => x.Id == wishlistId)?.Items.FirstOrDefault(i => i.Id == itemId);
        if (item is not null) item.IsFulfilled = fulfilled;
        await _store.CacheSetAsync(CacheKey, list);

        if (itemId > 0)
        {
            await EnqueueAsync(MutationKind.Update, $"items/{itemId}/fulfilled",
                new SetItemFulfilledRequest { Fulfilled = fulfilled });
        }
    }

    public async Task DeleteItemAsync(int wishlistId, int itemId)
    {
        var list = await CachedAsync();
        var w = list.FirstOrDefault(x => x.Id == wishlistId);
        w?.Items.RemoveAll(i => i.Id == itemId);
        await _store.CacheSetAsync(CacheKey, list);

        if (itemId > 0)
        {
            await EnqueueAsync(MutationKind.Delete, $"items/{itemId}", null);
        }
    }

    // ---- replay (called by the SyncEngine) ----

    protected override async Task<bool> ReplayAsync(OutboxMutation m)
    {
        try
        {
            HttpResponseMessage response = (m.Kind, m.EntityId) switch
            {
                // wishlist create
                (MutationKind.Create, null) =>
                    await _http.PostAsync("api/wishlists", Body(m.PayloadJson)),
                // item create (EntityId == "items")
                (MutationKind.Create, "items") =>
                    await _http.PostAsync("api/wishlists/items", Body(m.PayloadJson)),
                // item fulfilled update (EntityId == "items/{id}/fulfilled")
                (MutationKind.Update, var p) when p!.StartsWith("items/") =>
                    await _http.PutAsync($"api/wishlists/{p}", Body(m.PayloadJson)),
                // delete (wishlist "{id}" or item "items/{id}")
                (MutationKind.Delete, var p) =>
                    await _http.DeleteAsync($"api/wishlists/{p}"),
                _ => throw new InvalidOperationException("Unbekannte Mutation.")
            };

            if (response.IsSuccessStatusCode || (int)response.StatusCode is >= 400 and < 500)
            {
                await _store.OutboxRemoveAsync(m.Id);
                return true;
            }
        }
        catch { /* keep queued */ }
        return false;
    }

    private static StringContent Body(string? json)
        => new(json ?? "{}", Encoding.UTF8, "application/json");
}
