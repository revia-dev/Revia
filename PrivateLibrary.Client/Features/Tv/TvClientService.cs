using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using PrivateLibrary.Client.Offline;
using PrivateLibrary.Shared.Offline;
using PrivateLibrary.Shared.Tv;

namespace PrivateLibrary.Client.Features.Tv;

/// <summary>
/// Offline-capable TV guide. Two server-filtered views (private / family) are cached
/// separately so both work offline; the movie picker options are cached too. Writes
/// (add private/household, toggle share, delete) apply optimistically to the private
/// cache and queue in the outbox, syncing on reconnect (server-wins).
/// </summary>
public sealed class TvClientService
{
    public const string Collection = "tv";
    private const string PrivateKey = "col:tv-private";
    private const string FamilyKey = "col:tv-family";
    private const string OptionsKey = "col:tv-options";

    private readonly HttpClient _http;
    private readonly OfflineStore _store;
    private readonly ConnectivityService _connectivity;
    private readonly SyncEngine _sync;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public TvClientService(IHttpClientFactory httpFactory, OfflineStore store,
        ConnectivityService connectivity, SyncEngine sync)
    {
        _http = httpFactory.CreateClient("api");
        _store = store;
        _connectivity = connectivity;
        _sync = sync;
        _sync.RegisterHandler(Collection, ReplayAsync);
    }

    public Task<List<TvEntryDto>> GetPrivateAsync() => GetCachedOrFetchAsync(PrivateKey, "api/tv/private");
    public Task<List<TvEntryDto>> GetFamilyAsync() => GetCachedOrFetchAsync(FamilyKey, "api/tv/family");

    public async Task<List<TvMovieOptionDto>> GetMovieOptionsAsync()
    {
        if (_connectivity.IsOnline)
        {
            try
            {
                var fresh = await _http.GetFromJsonAsync<List<TvMovieOptionDto>>("api/tv/movie-options");
                if (fresh is not null)
                {
                    await _store.CacheSetAsync(OptionsKey, fresh);
                    return fresh;
                }
            }
            catch { }
        }
        return await _store.CacheGetAsync<List<TvMovieOptionDto>>(OptionsKey) ?? new();
    }

    private async Task<List<TvEntryDto>> GetCachedOrFetchAsync(string key, string path)
    {
        if (_connectivity.IsOnline)
        {
            try
            {
                var fresh = await _http.GetFromJsonAsync<List<TvEntryDto>>(path);
                if (fresh is not null)
                {
                    await _store.CacheSetAsync(key, fresh);
                    return fresh;
                }
            }
            catch { }
        }
        return await _store.CacheGetAsync<List<TvEntryDto>>(key) ?? new();
    }

    public async Task AddPrivateAsync(Guid movieId, string movieTitle, DateTime startsAt, string? channel, string? note, bool shared)
    {
        var list = await _store.CacheGetAsync<List<TvEntryDto>>(PrivateKey) ?? new();
        list.Add(new TvEntryDto
        {
            Id = TempId(), MovieId = movieId, MovieTitle = movieTitle,
            StartsAt = startsAt, Channel = channel, Note = note,
            SharedWithHousehold = shared, HasPrivateOwner = true
        });
        await _store.CacheSetAsync(PrivateKey, list);

        await EnqueueAsync(MutationKind.Create, null, new AddTvEntryRequest
        {
            MovieId = movieId, StartsAt = startsAt, Channel = channel, Note = note,
            Shared = shared, Household = false
        });
    }

    public async Task SetSharedAsync(int id, bool shared)
    {
        var list = await _store.CacheGetAsync<List<TvEntryDto>>(PrivateKey) ?? new();
        var e = list.FirstOrDefault(x => x.Id == id);
        if (e is not null) e.SharedWithHousehold = shared;
        await _store.CacheSetAsync(PrivateKey, list);

        if (id > 0)
        {
            await EnqueueAsync(MutationKind.Update, $"{id}/shared", new SetTvSharedRequest { Shared = shared });
        }
    }

    public async Task DeleteAsync(int id)
    {
        foreach (var key in new[] { PrivateKey, FamilyKey })
        {
            var list = await _store.CacheGetAsync<List<TvEntryDto>>(key) ?? new();
            list.RemoveAll(x => x.Id == id);
            await _store.CacheSetAsync(key, list);
        }
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
            HttpResponseMessage response = (m.Kind, m.EntityId) switch
            {
                (MutationKind.Create, _) => await _http.PostAsync("api/tv", Body(m.PayloadJson)),
                (MutationKind.Update, var p) when p!.EndsWith("/shared") =>
                    await _http.PutAsync($"api/tv/{p}", Body(m.PayloadJson)),
                (MutationKind.Delete, var p) => await _http.DeleteAsync($"api/tv/{p}"),
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
