using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using PrivateLibrary.Client.Offline;
using PrivateLibrary.Shared.Games;
using PrivateLibrary.Shared.Offline;
using PrivateLibrary.Shared.Persons;

namespace PrivateLibrary.Client.Features.Games;

/// <summary>
/// Offline-capable game matches (step 2). Matches are cached per game; create/
/// update/complete/delete apply optimistically and queue in the outbox, syncing on
/// reconnect (server-wins). Person lookup is cached so the participant picker works
/// offline. Match photos are deferred to a later hardening step.
/// </summary>
public sealed class GameMatchClientService
{
    public const string Collection = "game-matches";

    private readonly HttpClient _http;
    private readonly OfflineStore _store;
    private readonly ConnectivityService _connectivity;
    private readonly SyncEngine _sync;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public GameMatchClientService(IHttpClientFactory httpFactory, OfflineStore store,
        ConnectivityService connectivity, SyncEngine sync)
    {
        _http = httpFactory.CreateClient("api");
        _store = store;
        _connectivity = connectivity;
        _sync = sync;
        _sync.RegisterHandler(Collection, ReplayAsync);
    }

    private static string Key(int gameId) => $"col:matches:{gameId}";

    public async Task<List<GameMatchDto>> GetForGameAsync(int gameId)
    {
        if (_connectivity.IsOnline)
        {
            try
            {
                var fresh = await _http.GetFromJsonAsync<List<GameMatchDto>>($"api/games/{gameId}/matches");
                if (fresh is not null)
                {
                    await _store.CacheSetAsync(Key(gameId), fresh);
                    return fresh;
                }
            }
            catch { }
        }
        return await _store.CacheGetAsync<List<GameMatchDto>>(Key(gameId)) ?? new();
    }

    public async Task<List<PersonLookupDto>> GetPersonsAsync()
    {
        if (_connectivity.IsOnline)
        {
            try
            {
                var fresh = await _http.GetFromJsonAsync<List<PersonLookupDto>>("api/persons/lookup");
                if (fresh is not null)
                {
                    await _store.CacheSetAsync("col:persons", fresh);
                    return fresh;
                }
            }
            catch { }
        }
        return await _store.CacheGetAsync<List<PersonLookupDto>>("col:persons") ?? new();
    }

    public async Task SaveAsync(SaveMatchRequest request, Dictionary<string, string> personNames)
    {
        var list = await _store.CacheGetAsync<List<GameMatchDto>>(Key(request.GameId)) ?? new();
        var optimistic = new GameMatchDto
        {
            Id = request.Id != 0 ? request.Id : TempId(),
            GameId = request.GameId,
            Title = request.Title,
            PlayedOn = request.PlayedOn == default ? DateTime.Today : request.PlayedOn,
            ScoreDefinitionId = request.ScoreDefinitionId,
            Notes = request.Notes,
            Participants = request.Participants.Select(p => new MatchParticipantDto
            {
                PersonId = p.PersonId,
                PersonDisplayName = personNames.TryGetValue(p.PersonId, out var n) ? n : "",
                Score = p.Score
            }).ToList()
        };

        var idx = list.FindIndex(m => m.Id == optimistic.Id);
        if (idx >= 0) { list[idx] = optimistic; } else { list.Add(optimistic); }
        await _store.CacheSetAsync(Key(request.GameId), list);

        if (request.Id > 0)
        {
            await EnqueueAsync(MutationKind.Update, $"matches/{request.Id}", request);
        }
        else
        {
            await EnqueueAsync(MutationKind.Create, $"{request.GameId}/matches", request);
        }
    }

    public async Task SetCompletedAsync(int gameId, int matchId, bool completed)
    {
        var list = await _store.CacheGetAsync<List<GameMatchDto>>(Key(gameId)) ?? new();
        var m = list.FirstOrDefault(x => x.Id == matchId);
        if (m is not null) m.IsCompleted = completed;
        await _store.CacheSetAsync(Key(gameId), list);

        if (matchId > 0)
        {
            await EnqueueAsync(MutationKind.Update, $"matches/{matchId}/completed",
                new SetMatchCompletedRequest { Completed = completed });
        }
    }

    public async Task DeleteAsync(int gameId, int matchId)
    {
        var list = await _store.CacheGetAsync<List<GameMatchDto>>(Key(gameId)) ?? new();
        list.RemoveAll(m => m.Id == matchId);
        await _store.CacheSetAsync(Key(gameId), list);

        if (matchId > 0)
        {
            await EnqueueAsync(MutationKind.Delete, $"matches/{matchId}", null);
        }
    }

    private async Task EnqueueAsync(MutationKind kind, string path, object? payload)
    {
        await _store.OutboxAddAsync(new OutboxMutation
        {
            Collection = Collection, Kind = kind, EntityId = path,
            PayloadJson = payload is null ? null : JsonSerializer.Serialize(payload, Json)
        });
        _sync.NotifyQueued();
        if (_connectivity.IsOnline)
        {
            await _sync.FlushAsync();
        }
    }

    private async Task<bool> ReplayAsync(OutboxMutation m)
    {
        try
        {
            // EntityId carries the path suffix: create -> "{gameId}/matches",
            // update -> "matches/{id}" or "matches/{id}/completed", delete -> "matches/{id}".
            HttpResponseMessage response = m.Kind switch
            {
                MutationKind.Create => await _http.PostAsync($"api/games/{m.EntityId}", Body(m.PayloadJson)),
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
