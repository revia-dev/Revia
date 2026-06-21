using System.ComponentModel.DataAnnotations;

namespace Revia.Data;

/// <summary>
/// A linear "TV guide" a household member assembles from the household's movies.
/// Each member has one private program (<see cref="OwnerUserId"/> set) and the
/// household has one shared "family" program (<see cref="OwnerUserId"/> null).
/// </summary>
public sealed class TvProgram
{
    public int Id { get; set; }

    public int HouseholdId { get; set; }

    /// <summary>Owning member for a private program; null for the household program.</summary>
    public string? OwnerUserId { get; set; }

    public ApplicationUser? OwnerUser { get; set; }

    [Required]
    [StringLength(150)]
    public string Name { get; set; } = string.Empty;

    public ICollection<TvProgramEntry> Entries { get; set; } = new List<TvProgramEntry>();
}

/// <summary>A single scheduled slot in a <see cref="TvProgram"/>.</summary>
public sealed class TvProgramEntry
{
    public int Id { get; set; }

    public int TvProgramId { get; set; }
    public TvProgram? TvProgram { get; set; }

    public Guid MovieId { get; set; }
    public Movie? Movie { get; set; }

    /// <summary>Scheduled date and time of this slot (the program is shown sorted by it).</summary>
    public DateTime StartsAt { get; set; }

    [StringLength(80)]
    public string? Channel { get; set; }

    [StringLength(500)]
    public string? Note { get; set; }

    /// <summary>For a private entry: when true it is also shown in the household program.</summary>
    public bool SharedWithHousehold { get; set; }
}
