namespace PrivateLibrary.Shared.Dashboard;

/// <summary>Per-household counts + lending KPIs for the dashboard.</summary>
public sealed class DashboardDto
{
    // Module tile counts.
    public int Movies { get; set; }
    public int Games { get; set; }
    public int OpenMatches { get; set; }
    public int BmiRecords { get; set; }
    public int Wishlists { get; set; }
    public int OpenWishes { get; set; }
    public int TvEntries { get; set; }
    public int Persons { get; set; }

    // Lending KPIs.
    public int ActiveLendings { get; set; }
    public int OverdueLendings { get; set; }
    public int ReturnedLendings { get; set; }
}
