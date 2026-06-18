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

    /// <summary>ISO timestamp before which this mutation should not be retried
    /// (exponential backoff after a transient failure). Null = retry immediately.</summary>
    public string? NextAttemptAt { get; set; }

    /// <summary>Set once the attempt cap is reached: the mutation keeps failing
    /// transiently. It is skipped (so it can't block the queue) and surfaced to the
    /// user, but kept so it can be retried manually.</summary>
    public bool Poison { get; set; }

    /// <summary>Last replay error (HTTP status or exception message), for diagnostics.</summary>
    public string? LastError { get; set; }
}

/// <summary>Result of attempting to flush the outbox.</summary>
/// <param name="Sent">Mutations successfully replayed (or dropped as server-wins).</param>
/// <param name="Failed">Mutations that failed transiently this pass.</param>
/// <param name="Remaining">Mutations still queued (includes poisoned).</param>
/// <param name="Poisoned">Of those remaining, how many are poisoned (cap reached).</param>
public sealed record SyncResult(int Sent, int Failed, int Remaining, int Poisoned = 0);
