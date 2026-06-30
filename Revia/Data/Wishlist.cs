using System.ComponentModel.DataAnnotations;

namespace Revia.Data;

/// <summary>A named wish list owned by a household member. Visible to the whole household.</summary>
public sealed class Wishlist
{
    public int Id { get; set; }
    public int HouseholdId { get; set; }

    public string OwnerUserId { get; set; } = string.Empty;
    public ApplicationUser? OwnerUser { get; set; }

    [Required]
    [StringLength(150)]
    public string Name { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public ICollection<WishlistItem> Items { get; set; } = new List<WishlistItem>();
}

/// <summary>A single wish in a <see cref="Wishlist"/>.</summary>
public sealed class WishlistItem
{
    public int Id { get; set; }
    public int WishlistId { get; set; }
    public Wishlist? Wishlist { get; set; }

    [Required]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Url { get; set; }

    [StringLength(1000)]
    public string? Notes { get; set; }

    public decimal? Price { get; set; }

    public bool IsFulfilled { get; set; }

    /// <summary>Optional inline photo for the wish (small images; mirrors the native
    /// app's per-item photo). Stored inline so it round-trips through the backup as bytes.</summary>
    public byte[]? PhotoData { get; set; }

    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; }
}
