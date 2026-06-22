using Revia.Shared.Offline;

namespace Revia.Client.Offline;

/// <summary>
/// The outbox operations the <see cref="SyncEngine"/> depends on. Extracted from
/// <see cref="OfflineStore"/> (the IndexedDB-backed implementation) so the engine's
/// replay/backoff/poison logic can be unit-tested headlessly, without a browser.
/// </summary>
public interface IOutboxStore
{
    Task<List<OutboxMutation>> OutboxAllAsync();
    Task OutboxUpdateAsync(OutboxMutation mutation);
    Task OutboxRemoveAsync(string id);
    Task<int> OutboxCountAsync();
}

/// <summary>
/// The connectivity signal the <see cref="SyncEngine"/> depends on. Implemented by
/// <see cref="ConnectivityService"/> over the browser's online/offline events; a fake
/// drives it in tests.
/// </summary>
public interface IConnectivity
{
    bool IsOnline { get; }
    event Action<bool>? Changed;
}
