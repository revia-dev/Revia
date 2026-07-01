using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Revia.Client.Offline;
using Revia.Shared.Offline;
using Revia.Shared.Progress;

namespace Revia.Client.Features.Progress;

/// <summary>
/// Offline-capable per-user watch progress. The signed-in user's progress for each
/// movie/series is read from the API (and cached), and writes are applied
/// optimistically to the cache and queued. Every write is an upsert keyed by movie,
/// so the replay always PUTs to <c>api/progress</c> and a queued change for the same
/// movie collapses to the latest value.
/// </summary>
public sealed class WatchProgressClientService : OfflineClientService
{
    public const string Collection = "progress";
    private const string CacheKey = "col:progress";

    public WatchProgressClientService(IHttpClientFactory httpFactory, OfflineStore store,
        ConnectivityService connectivity, SyncEngine sync)
        : base(Collection, httpFactory, store, connectivity, sync) { }

    /// <summary>All of the current user's progress, refreshed from the API when online.</summary>
    public async Task<List<WatchProgressDto>> GetAllAsync()
    {
        if (_connectivity.IsOnline)
        {
            try
            {
                var fresh = await _http.GetFromJsonAsync<List<WatchProgressDto>>("api/progress");
                if (fresh is not null)
                {
                    await _store.CacheSetAsync(CacheKey, fresh);
                    return fresh;
                }
            }
            catch { /* fall through to cache */ }
        }
        return await _store.CacheGetAsync<List<WatchProgressDto>>(CacheKey) ?? new();
    }

    public async Task<List<WatchProgressDto>> GetCachedAsync()
        => await _store.CacheGetAsync<List<WatchProgressDto>>(CacheKey) ?? new();

    /// <summary>The cached progress for one movie, or null if none recorded yet.</summary>
    public async Task<WatchProgressDto?> GetForMovieAsync(Guid movieId)
        => (await GetCachedAsync()).FirstOrDefault(p => p.MovieId == movieId);

    /// <summary>Upserts progress for one movie: updates the cache immediately and
    /// queues the change. The same movie collapses to one cached entry.</summary>
    public async Task SaveAsync(SaveWatchProgressRequest request)
    {
        request.Status = WatchStatus.Normalize(request.Status);

        var list = await GetCachedAsync();
        var idx = list.FindIndex(p => p.MovieId == request.MovieId);
        var dto = new WatchProgressDto
        {
            MovieId = request.MovieId,
            Status = request.Status,
            PositionSeconds = request.Season is null && request.Episode is null ? request.PositionSeconds : null,
            Season = request.Season,
            Episode = request.Episode,
            UpdatedAt = DateTime.UtcNow,
        };
        if (idx >= 0) { list[idx] = dto; } else { list.Add(dto); }
        await _store.CacheSetAsync(CacheKey, list);

        await EnqueueAsync(MutationKind.Update, request.MovieId.ToString(), request);
    }

    public async Task DeleteAsync(Guid movieId)
    {
        var list = await GetCachedAsync();
        list.RemoveAll(p => p.MovieId == movieId);
        await _store.CacheSetAsync(CacheKey, list);
        await EnqueueAsync(MutationKind.Delete, movieId.ToString(), null);
    }

    protected override async Task<bool> ReplayAsync(OutboxMutation m)
    {
        try
        {
            HttpResponseMessage response = m.Kind switch
            {
                // Create and Update are both upserts against the same endpoint.
                MutationKind.Create or MutationKind.Update
                    => await _http.PutAsync("api/progress", Body(m.PayloadJson)),
                MutationKind.Delete
                    => await _http.DeleteAsync($"api/progress/{m.EntityId}"),
                _ => throw new InvalidOperationException()
            };
            if (response.IsSuccessStatusCode || (int)response.StatusCode is >= 400 and < 500)
            {
                await _store.OutboxRemoveAsync(m.Id);
                return true;
            }
        }
        catch { /* keep queued for the next sync */ }
        return false;
    }

    private static StringContent Body(string? json) => new(json ?? "{}", Encoding.UTF8, "application/json");
}
