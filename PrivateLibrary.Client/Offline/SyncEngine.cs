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

    /// <summary>After this many transient failures a mutation is marked poison and
    /// skipped, so it can't block everything queued behind it.</summary>
    private const int MaxAttempts = 6;

    /// <summary>Exponential backoff per attempt, capped — so a flush doesn't hammer
    /// a struggling server and a transient failure gets progressively more patient.</summary>
    private static TimeSpan Backoff(int attempts)
    {
        var seconds = Math.Min(300, Math.Pow(2, Math.Min(attempts, 8)));
        return TimeSpan.FromSeconds(seconds);
    }

    /// <summary>Raised after a flush attempt completes (with how much was sent/left).</summary>
    public event Action<SyncResult>? Synced;

    /// <summary>Raised when a mutation is enqueued (so a global indicator can update
    /// its pending count even while offline, before any flush happens).</summary>
    public event Action? Queued;

    /// <summary>Called by collection services right after enqueuing a mutation.</summary>
    public void NotifyQueued() => Queued?.Invoke();

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

    /// <summary>How many queued mutations are poisoned (repeatedly failed, now skipped).</summary>
    public async Task<int> PoisonCountAsync()
        => (await _store.OutboxAllAsync()).Count(m => m.Poison);

    /// <summary>Clears the poison flag + backoff on all stuck mutations so the next
    /// flush retries them (e.g. after the user fixes connectivity or the server).</summary>
    public async Task RetryPoisonedAsync()
    {
        foreach (var m in await _store.OutboxAllAsync())
        {
            if (m.Poison || m.NextAttemptAt is not null)
            {
                m.Poison = false;
                m.Attempts = 0;
                m.NextAttemptAt = null;
                await _store.OutboxUpdateAsync(m);
            }
        }
        await FlushAsync();
    }

    /// <summary>Replays queued mutations FIFO. Stops early if offline. Skips mutations
    /// that are poisoned or still within their backoff window; on a transient failure
    /// a mutation is backed off and eventually marked poison so it can't wedge the queue.</summary>
    public async Task<SyncResult> FlushAsync()
    {
        if (_running || !_connectivity.IsOnline)
        {
            var remaining = await _store.OutboxAllAsync();
            return new SyncResult(0, 0, remaining.Count, remaining.Count(m => m.Poison));
        }

        _running = true;
        var sent = 0;
        var failed = 0;
        try
        {
            var now = DateTimeOffset.UtcNow;
            var mutations = await _store.OutboxAllAsync();
            foreach (var m in mutations)
            {
                if (!_connectivity.IsOnline)
                {
                    break;
                }

                // Skip poisoned and not-yet-due mutations, but keep scanning so a
                // healthy mutation behind a stuck one still gets a chance.
                if (m.Poison)
                {
                    continue;
                }
                if (m.NextAttemptAt is not null
                    && DateTimeOffset.TryParse(m.NextAttemptAt, out var due) && due > now)
                {
                    continue;
                }

                if (!_handlers.TryGetValue(m.Collection, out var replay))
                {
                    // Unknown collection (e.g. removed module) — drop to avoid a stuck queue.
                    await _store.OutboxRemoveAsync(m.Id);
                    continue;
                }

                bool ok;
                try
                {
                    ok = await replay(m);
                }
                catch (Exception ex)
                {
                    ok = false;
                    m.LastError = ex.Message;
                }

                if (ok)
                {
                    sent++;
                }
                else
                {
                    failed++;
                    await RecordFailureAsync(m, now);
                }
            }
        }
        finally
        {
            _running = false;
        }

        var left = await _store.OutboxAllAsync();
        var result = new SyncResult(sent, failed, left.Count, left.Count(m => m.Poison));
        Synced?.Invoke(result);
        return result;
    }

    /// <summary>Records a transient failure: bumps the attempt count, sets the next
    /// retry time (backoff), and marks the mutation poison once the cap is reached.</summary>
    private async Task RecordFailureAsync(OutboxMutation m, DateTimeOffset now)
    {
        m.Attempts++;
        if (m.Attempts >= MaxAttempts)
        {
            m.Poison = true;
            m.NextAttemptAt = null;
        }
        else
        {
            m.NextAttemptAt = now.Add(Backoff(m.Attempts)).ToString("O");
        }
        await _store.OutboxUpdateAsync(m);
    }
}
