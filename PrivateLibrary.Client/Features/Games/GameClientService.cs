using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using PrivateLibrary.Client.Offline;
using PrivateLibrary.Shared.Games;
using PrivateLibrary.Shared.Offline;

namespace PrivateLibrary.Client.Features.Games;

/// <summary>
/// Offline-capable games collection (step 1: the game + its types/score
/// definitions). Reads cached; create/update/delete apply optimistically and
/// queue in the outbox, syncing on reconnect (server-wins). Matches/files come
/// in a later step.
/// </summary>
public sealed class GameClientService
{
    public const string Collection = "games";
    private const string CacheKey = "col:games";

    private readonly HttpClient _http;
    private readonly OfflineStore _store;
    private readonly ConnectivityService _connectivity;
    private readonly SyncEngine _sync;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public GameClientService(IHttpClientFactory httpFactory, OfflineStore store,
        ConnectivityService connectivity, SyncEngine sync)
    {
        _http = httpFactory.CreateClient("api");
        _store = store;
        _connectivity = connectivity;
        _sync = sync;
        _sync.RegisterHandler(Collection, ReplayAsync);
    }

    public async Task<List<GameDto>> GetAllAsync()
    {
        if (_connectivity.IsOnline)
        {
            try
            {
                var fresh = await _http.GetFromJsonAsync<List<GameDto>>("api/games");
                if (fresh is not null)
                {
                    await _store.CacheSetAsync(CacheKey, fresh);
                    return fresh;
                }
            }
            catch { }
        }
        return await _store.CacheGetAsync<List<GameDto>>(CacheKey) ?? new();
    }

    public async Task SaveAsync(SaveGameRequest request)
    {
        var list = await _store.CacheGetAsync<List<GameDto>>(CacheKey) ?? new();
        var optimistic = new GameDto
        {
            Id = request.Id != 0 ? request.Id : TempId(),
            Name = request.Name,
            Publisher = request.Publisher,
            ReleaseYear = request.ReleaseYear,
            MinPlayers = request.MinPlayers,
            MaxPlayers = request.MaxPlayers,
            Description = request.Description,
            Types = request.Types.ToList(),
            ScoreDefinitions = request.ScoreDefinitions.ToList()
        };

        var idx = list.FindIndex(g => g.Id == optimistic.Id);
        if (idx >= 0) { list[idx] = optimistic; } else { list.Add(optimistic); }
        await _store.CacheSetAsync(CacheKey, list);

        // Create (Id 0 or temp negative) vs update (real id).
        if (request.Id > 0)
        {
            await EnqueueAsync(MutationKind.Update, request.Id.ToString(), request);
        }
        else
        {
            await EnqueueAsync(MutationKind.Create, null, request);
        }
    }

    public async Task DeleteAsync(int id)
    {
        var list = await _store.CacheGetAsync<List<GameDto>>(CacheKey) ?? new();
        list.RemoveAll(g => g.Id == id);
        await _store.CacheSetAsync(CacheKey, list);
        if (id > 0)
        {
            await EnqueueAsync(MutationKind.Delete, id.ToString(), null);
        }
    }

    private async Task EnqueueAsync(MutationKind kind, string? entityId, object? payload)
    {
        await _store.OutboxAddAsync(new OutboxMutation
        {
            Collection = Collection, Kind = kind, EntityId = entityId,
            PayloadJson = payload is null ? null : JsonSerializer.Serialize(payload, Json)
        });
        if (_connectivity.IsOnline)
        {
            await _sync.FlushAsync();
        }
    }

    private async Task<bool> ReplayAsync(OutboxMutation m)
    {
        try
        {
            HttpResponseMessage response = m.Kind switch
            {
                MutationKind.Create => await _http.PostAsync("api/games", Body(m.PayloadJson)),
                MutationKind.Update => await _http.PutAsync($"api/games/{m.EntityId}", Body(m.PayloadJson)),
                MutationKind.Delete => await _http.DeleteAsync($"api/games/{m.EntityId}"),
                _ => throw new InvalidOperationException()
            };
            if (response.IsSuccessStatusCode || (int)response.StatusCode is >= 400 and < 500)
            {
                await _store.OutboxRemoveAsync(m.Id);
                return true;
            }
        }
        catch { }
        return false;
    }

    private static StringContent Body(string? json) => new(json ?? "{}", Encoding.UTF8, "application/json");

    private static int _temp = -1;
    private static int TempId() => _temp--;
}
