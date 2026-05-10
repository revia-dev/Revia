using System.ComponentModel.DataAnnotations;

namespace PrivateLibrary.Modules.Lending.Models;

public sealed class LendingItem
{
    public int Id { get; set; }

    public int LendingEntryId { get; set; }
    public LendingEntry? LendingEntry { get; set; }

    [Required]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    public int SortOrder { get; set; }
}
