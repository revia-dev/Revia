using PrivateLibrary.Shared.Offline;

namespace PrivateLibrary.Client.Offline;

/// <summary>
/// Flushes the offline outbox to the API. Replays queued mutations oldest-first
/// when connectivity returns (and on demand). Each collection registers a replay
/// handler so the engine can dispatch a mutation to the right repository. After a
/// successful flush it raises <see cref="Synced"/> so the UI can refresh from the
/// server (server-wins).
/// </summary>
public sealed class SyncEngine(OfflineStore store, ConnectivityService connectivity)
{
    private readonly OfflineStore _store = store;
    private readonly ConnectivityService _connectivity = connectivity;
    private readonly Dictionary<string, Func<OutboxMutation, Task<bool>>> _handlers = new(StringComparer.Ordinal);
    private bool _running;

    /// <summary>Raised after a flush attempt completes (with how much was sent/left).</summary>
    public event Action<SyncResult>? Synced;

    public bool Initialized { get; private set; }

    /// <summary>Registers how a collection's mutations are replayed. Called once per
    /// collection (typically from the collection's repository registration).</summary>
    public void RegisterHandler(string collection, Func<OutboxMutation, Task<bool>> replay)
        => _handlers[collection] = replay;

    /// <summary>Subscribes to connectivity changes; flushes once at startup if online.</summary>
    public async Task InitializeAsync()
    {
        if (Initialized)
        {
            return;
        }
        Initialized = true;

        _connectivity.Changed += online =>
        {
            if (online)
            {
                _ = FlushAsync();
            }
        };

        if (_connectivity.IsOnline)
        {
            await FlushAsync();
        }
    }

    public async Task<int> PendingCountAsync() => await _store.OutboxCountAsync();

    /// <summary>Replays all queued mutations FIFO. Stops early if offline.</summary>
    public async Task<SyncResult> FlushAsync()
    {
        if (_running || !_connectivity.IsOnline)
        {
            var remaining = await _store.OutboxCountAsync();
            return new SyncResult(0, 0, remaining);
        }

        _running = true;
        var sent = 0;
        var failed = 0;
        try
        {
            var mutations = await _store.OutboxAllAsync();
            foreach (var m in mutations)
            {
                if (!_connectivity.IsOnline)
                {
                    break;
                }
                if (!_handlers.TryGetValue(m.Collection, out var replay))
                {
                    // Unknown collection (e.g. removed module) — drop to avoid a stuck queue.
                    await _store.OutboxRemoveAsync(m.Id);
                    continue;
                }

                if (await replay(m))
                {
                    sent++;
                }
                else
                {
                    failed++;
                }
            }
        }
        finally
        {
            _running = false;
        }

        var result = new SyncResult(sent, failed, await _store.OutboxCountAsync());
        Synced?.Invoke(result);
        return result;
    }
}
