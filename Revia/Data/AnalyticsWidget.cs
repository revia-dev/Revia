using System.ComponentModel.DataAnnotations;

namespace Revia.Data;

/// <summary>
/// A user-defined dashboard analytics container, e.g. "Meist ausgeliehene Dinge"
/// or "Aktivste Kategorien". The user picks a metric and refines it with a few
/// options (stored as JSON), names it, and can edit/delete/reorder it. Evaluated
/// at render time by <c>AnalyticsService</c>.
/// </summary>
public sealed class AnalyticsWidget
{
    public int Id { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;

    public ApplicationUser? User { get; set; }

    /// <summary>Household scope, so a widget only ever reads the user's own data.</summary>
    public int HouseholdId { get; set; }

    /// <summary>User-chosen display name.</summary>
    [Required, StringLength(80)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Metric key from <c>AnalyticsMetrics</c> (e.g. "lending.most-borrowed").</summary>
    [Required, StringLength(60)]
    public string Metric { get; set; } = string.Empty;

    /// <summary>JSON bag of metric options (top-N, status filter, time range, …).</summary>
    [StringLength(2000)]
    public string? OptionsJson { get; set; }

    /// <summary>Display order on the dashboard.</summary>
    public int SortOrder { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
