namespace PrivateLibrary.Shared.Offline;

/// <summary>The kind of operation a queued mutation represents.</summary>
public enum MutationKind { Create, Update, Delete }

/// <summary>
/// A queued offline mutation. Stored in the outbox and replayed against the API
/// when connectivity returns. Idempotent via <see cref="Id"/> (a client-generated
/// GUID) so a replay that partially succeeded can be retried safely.
/// </summary>
public sealed class OutboxMutation
{
    /// <summary>Client-generated id; also the outbox key.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Logical collection, e.g. "wishlists" — selects the API endpoint.</summary>
    public string Collection { get; set; } = string.Empty;

    public MutationKind Kind { get; set; }

    /// <summary>Server id of the affected entity for Update/Delete (string form).</summary>
    public string? EntityId { get; set; }

    /// <summary>For Create/Update: the JSON payload (a request DTO).</summary>
    public string? PayloadJson { get; set; }

    /// <summary>ISO timestamp; the outbox replays oldest-first.</summary>
    public string CreatedAt { get; set; } = DateTimeOffset.UtcNow.ToString("O");

    /// <summary>How many replay attempts have failed (for backoff/diagnostics).</summary>
    public int Attempts { get; set; }
}

/// <summary>Result of attempting to flush the outbox.</summary>
public sealed record SyncResult(int Sent, int Failed, int Remaining);
