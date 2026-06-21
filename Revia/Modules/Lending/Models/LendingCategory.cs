using System.ComponentModel.DataAnnotations;

namespace Revia.Modules.Lending.Models;

public sealed class LendingCategory
{
    public int Id { get; set; }

    /// <summary>Owning household (tenant). Category names are unique per household.</summary>
    public int HouseholdId { get; set; }

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ICollection<LendingEntryCategory> EntryLinks { get; set; } = new List<LendingEntryCategory>();
}
