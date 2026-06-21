namespace Revia.Data;

/// <summary>
/// A bidirectional link between two households, created when a person's QR code is
/// scanned and imported into another household. Linked households can see each
/// other's lending entries. The pair is stored normalized (lower id first) and
/// deduplicated by a unique index.
/// </summary>
public sealed class HouseholdLink
{
    public int Id { get; set; }

    public int HouseholdAId { get; set; }
    public int HouseholdBId { get; set; }

    public DateTime CreatedAt { get; set; }

    /// <summary>Returns the pair ordered so the smaller id is A.</summary>
    public static (int a, int b) Normalize(int x, int y) => x <= y ? (x, y) : (y, x);
}
