namespace PrivateLibrary.Shared.Games;

/// <summary>
/// A points/money transfer between two players in a running match (card game,
/// board game, …). Encoded compactly into a QR code / share payload so one device can
/// hand points to another. The receiving device applies it: sender −Amount,
/// receiver +Amount. <see cref="TransactionId"/> makes it idempotent so the same QR
/// can't be applied twice.
/// </summary>
public sealed class PointTransfer
{
    /// <summary>Unique id (client GUID) — used to ignore a re-scanned transfer.</summary>
    public string TransactionId { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>The match this transfer belongs to.</summary>
    public int MatchId { get; set; }

    /// <summary>Sender / receiver person ids.</summary>
    public string FromPersonId { get; set; } = string.Empty;
    public string ToPersonId { get; set; } = string.Empty;

    /// <summary>Amount moved from sender to receiver (always positive).</summary>
    public double Amount { get; set; }

    /// <summary>Display names so the receiving device can show who/what without a
    /// lookup (the other device may belong to another household).</summary>
    public string? FromName { get; set; }
    public string? ToName { get; set; }
}
