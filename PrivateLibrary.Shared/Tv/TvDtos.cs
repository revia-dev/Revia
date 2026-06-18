using System.ComponentModel.DataAnnotations;

namespace PrivateLibrary.Shared.Tv;

/// <summary>A TV-guide entry as returned to the client.</summary>
public sealed class TvEntryDto
{
    public int Id { get; set; }
    public Guid MovieId { get; set; }
    public string MovieTitle { get; set; } = string.Empty;
    public string? PosterPath { get; set; }
    public DateTime StartsAt { get; set; }
    public string? Channel { get; set; }
    public string? Note { get; set; }
    public bool SharedWithHousehold { get; set; }
    /// <summary>Owner display name (shown in the family view for members' shared entries).</summary>
    public string? OwnerDisplayName { get; set; }
    /// <summary>True when this entry has a private owner (vs. the household program itself).</summary>
    public bool HasPrivateOwner { get; set; }
}

/// <summary>A movie option for the TV add-picker.</summary>
public sealed class TvMovieOptionDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Year { get; set; }
}

public sealed class AddTvEntryRequest
{
    [Required]
    public Guid MovieId { get; set; }
    public DateTime StartsAt { get; set; }
    public string? Channel { get; set; }
    public string? Note { get; set; }
    /// <summary>Private add: share with the household. Ignored for household adds.</summary>
    public bool Shared { get; set; }
    /// <summary>True to add to the household program directly; false for the user's private program.</summary>
    public bool Household { get; set; }
}

public sealed class SetTvSharedRequest
{
    public bool Shared { get; set; }
}
