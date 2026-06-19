using PrivateLibrary.Client.Offline;
using PrivateLibrary.Shared.Movies;

namespace PrivateLibrary.Client.Features.Movies;

/// <summary>
/// Offline-capable movie data access. Thin wrapper over the generic
/// OfflineRepository: reads are cached (large list works offline), writes are
/// optimistic + queued, and the SyncEngine replays them on reconnect (server-wins).
/// </summary>
public sealed class MovieClientService
{
    private readonly OfflineRepository<MovieDto> _repo;

    public MovieClientService(
        IHttpClientFactory httpFactory,
        OfflineStore store,
        ConnectivityService connectivity,
        SyncEngine sync)
    {
        var collection = new MovieCollection();
        _repo = new OfflineRepository<MovieDto>(httpFactory, store, connectivity, collection);
        // Register replay so the sync engine can flush queued movie mutations.
        sync.RegisterHandler(collection.Name, _repo.TryReplayAsync);
    }

    public Task<List<MovieDto>> GetAllAsync() => _repo.GetAllAsync();

    /// <summary>Reads movies from the local cache only (no network round-trip), for
    /// fast secondary views like the detail page.</summary>
    public Task<List<MovieDto>> GetCachedAsync() => _repo.GetCachedAsync();

    public Task CreateAsync(SaveMovieRequest request, MovieDto optimistic)
        => _repo.CreateAsync(request, optimistic);

    public Task UpdateAsync(SaveMovieRequest request, MovieDto optimistic)
        => _repo.UpdateAsync(optimistic.Id.ToString(), request, optimistic);

    public Task DeleteAsync(Guid id) => _repo.DeleteAsync(id.ToString());
}
