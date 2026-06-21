using System.ComponentModel.DataAnnotations;

namespace Revia.Modules.Lending.Models;

public sealed class LendingPhoto
{
    public int Id { get; set; }

    public int LendingEntryId { get; set; }
    public LendingEntry? LendingEntry { get; set; }

    /// <summary>Generated file name on disk (e.g. "{guid}.jpg"). Not the original name.</summary>
    [Required]
    [StringLength(260)]
    public string FileName { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string ContentType { get; set; } = string.Empty;

    [StringLength(260)]
    public string? OriginalName { get; set; }

    public long SizeBytes { get; set; }

    public int SortOrder { get; set; }

    public DateTime CreatedAt { get; set; }
}
