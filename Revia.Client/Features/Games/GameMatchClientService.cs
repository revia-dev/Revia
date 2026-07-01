using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Revia.Client.Offline;
using Revia.Shared.Games;
using Revia.Shared.Media;
using Revia.Shared.Offline;
using Revia.Shared.Persons;

namespace Revia.Client.Features.Games;

/// <summary>
/// Offline-capable game matches (step 2). Matches are cached per game; create/
/// update/complete/delete apply optimistically and queue in the outbox, syncing on
/// reconnect (server-wins). Person lookup is cached so the participant picker works
/// offline. Match photos are deferred to a later hardening step.
/// </summary>
public sealed class GameMatchClientService : OfflineClientService
{
    public const string Collection = "game-matches";

    public GameMatchClientService(IHttpClientFactory httpFactory, OfflineStore store,
        ConnectivityService connectivity, SyncEngine sync)
        : base(Collection, httpFactory, store, connectivity, sync) { }

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

    /// <summary>Attaches a photo to a match. Online-only — the photo needs the
    /// server-persisted match to exist. Returns true on success.</summary>
    public async Task<bool> UploadPhotoAsync(int matchId, UploadPhotoRequest request)
    {
        if (!_connectivity.IsOnline) return false;
        try
        {
            var r = await _http.PostAsJsonAsync($"api/games/matches/{matchId}/photos", request);
            return r.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    public async Task<bool> DeletePhotoAsync(int photoId)
    {
        if (!_connectivity.IsOnline) return false;
        try { return (await _http.DeleteAsync($"api/games/matches/photo/{photoId}")).IsSuccessStatusCode; }
        catch { return false; }
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

    protected override async Task<bool> ReplayAsync(OutboxMutation m)
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
}
