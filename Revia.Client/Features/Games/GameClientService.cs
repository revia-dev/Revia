using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Revia.Client.Offline;
using Revia.Shared.Games;
using Revia.Shared.Offline;

namespace Revia.Client.Features.Games;

/// <summary>
/// Offline-capable games collection (step 1: the game + its types/score
/// definitions). Reads cached; create/update/delete apply optimistically and
/// queue in the outbox, syncing on reconnect (server-wins). Matches/files come
/// in a later step.
/// </summary>
public sealed class GameClientService : OfflineClientService
{
    public const string Collection = "games";
    private const string CacheKey = "col:games";

    public GameClientService(IHttpClientFactory httpFactory, OfflineStore store,
        ConnectivityService connectivity, SyncEngine sync)
        : base(Collection, httpFactory, store, connectivity, sync) { }

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

    protected override async Task<bool> ReplayAsync(OutboxMutation m)
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
}
