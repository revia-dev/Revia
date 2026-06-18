namespace PrivateLibrary.Client.Offline;

/// <summary>
/// Describes how one logical collection (e.g. "wishlists") maps to its REST API,
/// so the generic OfflineRepository and SyncEngine can read/replay without knowing
/// the module. Implemented once per module (a few lines each).
/// </summary>
/// <typeparam name="TItem">The item DTO used in the read cache and lists.</typeparam>
public interface IOfflineCollection<TItem>
{
    /// <summary>Stable collection key — used for cache keys and the outbox routing.</summary>
    string Name { get; }

    /// <summary>Relative API path for the list/create endpoint, e.g. "api/wishlists".</summary>
    string ListPath { get; }

    /// <summary>Relative API path for a single item, e.g. "api/wishlists/{id}".</summary>
    string ItemPath(string id);

    /// <summary>Extracts the server id from an item (string form).</summary>
    string GetId(TItem item);
}
