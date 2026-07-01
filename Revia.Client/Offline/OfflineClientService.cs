using System.Text.Json;
using Revia.Shared.Offline;

namespace Revia.Client.Offline;

/// <summary>
/// Base for the offline-capable feature client services. Holds the shared plumbing each
/// of them used to duplicate: the "api" HttpClient, the offline store / connectivity /
/// sync engine, the JSON options, temporary-id generation, and the outbox enqueue (queue
/// a mutation, notify sync, flush if online). Subclasses pass their collection name and
/// implement <see cref="ReplayAsync"/> (how a queued mutation is sent to the server).
/// </summary>
public abstract class OfflineClientService
{
    protected readonly HttpClient _http;
    protected readonly OfflineStore _store;
    protected readonly ConnectivityService _connectivity;
    protected readonly SyncEngine _sync;
    protected static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly string _collection;
    private static int _temp = -1;

    protected OfflineClientService(string collection, IHttpClientFactory httpFactory,
        OfflineStore store, ConnectivityService connectivity, SyncEngine sync)
    {
        _collection = collection;
        _http = httpFactory.CreateClient("api");
        _store = store;
        _connectivity = connectivity;
        _sync = sync;
        _sync.RegisterHandler(collection, ReplayAsync);
    }

    /// <summary>Replays one queued mutation against the server (called by the sync engine).
    /// Returns true when the mutation is done (2xx or a 4xx that won't succeed on retry).</summary>
    protected abstract Task<bool> ReplayAsync(OutboxMutation mutation);

    /// <summary>Queues a mutation in the outbox, notifies sync, and flushes when online.</summary>
    protected async Task EnqueueAsync(MutationKind kind, string? entityId, object? payload)
    {
        await _store.OutboxAddAsync(new OutboxMutation
        {
            Collection = _collection,
            Kind = kind,
            EntityId = entityId,
            PayloadJson = payload is null ? null : JsonSerializer.Serialize(payload, Json)
        });
        _sync.NotifyQueued();
        if (_connectivity.IsOnline)
        {
            await _sync.FlushAsync();
        }
    }

    /// <summary>Negative, ever-decreasing id for optimistic inserts before the server assigns one.</summary>
    protected static int TempId() => _temp--;
}
