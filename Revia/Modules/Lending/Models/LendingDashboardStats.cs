namespace Revia.Modules.Lending.Models;

public sealed class LendingDashboardStats
{
    public int TotalActive { get; init; }
    public int TotalOverdue { get; init; }
    public int TotalReturned { get; init; }
    public int TotalPersons { get; init; }
    public IReadOnlyList<LendingEntry> RecentEntries { get; init; } = Array.Empty<LendingEntry>();
    public IReadOnlyList<LendingEntry> OverdueEntries { get; init; } = Array.Empty<LendingEntry>();
}
