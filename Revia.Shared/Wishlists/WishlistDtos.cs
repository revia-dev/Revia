using System.ComponentModel.DataAnnotations;

namespace Revia.Shared.Wishlists;

/// <summary>A wishlist with its items, as returned to the client.</summary>
public sealed class WishlistDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string OwnerUserId { get; set; } = string.Empty;
    public string OwnerDisplayName { get; set; } = string.Empty;
    public bool CanEdit { get; set; }
    public List<WishlistItemDto> Items { get; set; } = new();
}

public sealed class WishlistItemDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Url { get; set; }
    public string? Notes { get; set; }
    public decimal? Price { get; set; }
    public bool IsFulfilled { get; set; }
}

public sealed class CreateWishlistRequest
{
    [Required, StringLength(150)]
    public string Name { get; set; } = string.Empty;
}

public sealed class AddWishlistItemRequest
{
    [Required]
    public int WishlistId { get; set; }

    [Required, StringLength(200)]
    public string Title { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Url { get; set; }

    [StringLength(1000)]
    public string? Notes { get; set; }

    public decimal? Price { get; set; }
}

public sealed class SetItemFulfilledRequest
{
    public bool Fulfilled { get; set; }
}
