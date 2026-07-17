namespace Revia.Shared.Games;

/// <summary>
/// A points or money transfer in a running match, encoded into
/// a QR code / share payload so one device can hand points to others. It carries a
/// list of per-player deltas, so it supports 1..n players in a single payload
/// (e.g. one sender paying several receivers, or any multi-party adjustment). The
/// receiving device applies every delta for players present in its match.
/// <see cref="TransactionId"/> makes it idempotent so the same QR can't be applied
/// twice. Deltas always sum to zero (points are moved, never created/destroyed).
/// </summary>
public sealed class PointTransfer
{
    /// <summary>Unique id (client GUID) — used to ignore a re-scanned transfer.</summary>
    public string TransactionId { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>The match this transfer belongs to.</summary>
    public int MatchId { get; set; }

    /// <summary>One adjustment per affected player. Negative = pays, positive = receives.</summary>
    public List<PointTransferEntry> Entries { get; set; } = new();

    /// <summary>Optional human note (e.g. "Miete Schlossallee").</summary>
    public string? Note { get; set; }
}

/// <summary>A single player's delta within a <see cref="PointTransfer"/>.</summary>
public sealed class PointTransferEntry
{
    public string PersonId { get; set; } = string.Empty;
    /// <summary>Display name, so a device in another household can show it.</summary>
    public string? Name { get; set; }
    /// <summary>Signed amount: negative pays, positive receives.</summary>
    public double Delta { get; set; }
}
