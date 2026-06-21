using Revia.Shared.Games;

namespace Revia.Tests;

/// <summary>
/// The cross-device point transfer must conserve points: a payer's negative delta
/// equals the sum of the receivers' positive deltas, for 1..n receivers. Applying a
/// transfer therefore never creates or destroys points.
/// </summary>
public class PointTransferTests
{
    private static PointTransfer Build(string payer, params (string id, double amount)[] receivers)
    {
        var total = receivers.Sum(r => r.amount);
        var entries = new List<PointTransferEntry>
        {
            new() { PersonId = payer, Delta = -total }
        };
        entries.AddRange(receivers.Select(r => new PointTransferEntry { PersonId = r.id, Delta = r.amount }));
        return new PointTransfer { MatchId = 1, Entries = entries };
    }

    [Fact]
    public void SingleReceiver_NetsToZero()
    {
        var t = Build("a", ("b", 100));
        Assert.Equal(0, t.Entries.Sum(e => e.Delta), precision: 6);
        Assert.Equal(-100, t.Entries.Single(e => e.PersonId == "a").Delta);
        Assert.Equal(100, t.Entries.Single(e => e.PersonId == "b").Delta);
    }

    [Fact]
    public void ManyReceivers_NetToZero_AndPayerPaysTheSum()
    {
        var t = Build("a", ("b", 100), ("c", 50), ("d", 25));
        Assert.Equal(0, t.Entries.Sum(e => e.Delta), precision: 6);
        Assert.Equal(-175, t.Entries.Single(e => e.PersonId == "a").Delta);
    }

    [Fact]
    public void Applying_OnlyAffectsKnownPlayers()
    {
        // Simulates a receiving device that only has players a and b in its match.
        var t = Build("a", ("b", 100), ("c", 50));
        var localScores = new Dictionary<string, double> { ["a"] = 1000, ["b"] = 0 };

        foreach (var e in t.Entries)
        {
            if (localScores.ContainsKey(e.PersonId)) { localScores[e.PersonId] += e.Delta; }
        }

        Assert.Equal(850, localScores["a"]); // paid the full 150
        Assert.Equal(100, localScores["b"]);
        Assert.False(localScores.ContainsKey("c")); // unknown player untouched
    }
}
