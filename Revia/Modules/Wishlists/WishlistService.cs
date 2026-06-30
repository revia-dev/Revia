using Microsoft.EntityFrameworkCore;
using Revia.Data;
using Revia.Services;

namespace Revia.Modules.Wishlists;

/// <summary>
/// Household-scoped wishlists. Everyone in the household can view all lists;
/// only the owner may modify their own lists and wishes.
/// </summary>
public sealed class WishlistService(AppDbContext db, CurrentUserContext currentUser)
{
    private readonly AppDbContext _db = db;
    private readonly CurrentUserContext _currentUser = currentUser;

    public async Task<IReadOnlyList<Wishlist>> GetHouseholdWishlistsAsync(CancellationToken cancellationToken = default)
    {
        var householdId = await _currentUser.RequireHouseholdIdAsync();
        return await _db.Wishlists
            .AsNoTracking()
            .Where(x => x.HouseholdId == householdId)
            .Include(x => x.OwnerUser)
            .Include(x => x.Items)
            .OrderBy(x => x.OwnerUser!.LastName).ThenBy(x => x.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<Wishlist?> GetWishlistAsync(int id, CancellationToken cancellationToken = default)
    {
        var householdId = await _currentUser.RequireHouseholdIdAsync();
        return await _db.Wishlists
            .Include(x => x.OwnerUser)
            .Include(x => x.Items.OrderBy(i => i.SortOrder).ThenBy(i => i.Id))
            .FirstOrDefaultAsync(x => x.Id == id && x.HouseholdId == householdId, cancellationToken);
    }

    public async Task<bool> CanEditAsync(int wishlistId, CancellationToken cancellationToken = default)
    {
        var userId = await _currentUser.GetUserIdAsync();
        var householdId = await _currentUser.RequireHouseholdIdAsync();
        return await _db.Wishlists.AnyAsync(
            x => x.Id == wishlistId && x.HouseholdId == householdId && x.OwnerUserId == userId, cancellationToken);
    }

    public async Task<Wishlist> CreateWishlistAsync(string name, CancellationToken cancellationToken = default)
    {
        var householdId = await _currentUser.RequireHouseholdIdAsync();
        var userId = await _currentUser.GetUserIdAsync()
            ?? throw new InvalidOperationException("Kein angemeldeter Benutzer.");

        var wishlist = new Wishlist
        {
            HouseholdId = householdId,
            OwnerUserId = userId,
            Name = name.Trim(),
            CreatedAt = DateTime.UtcNow
        };
        _db.Wishlists.Add(wishlist);
        await _db.SaveChangesAsync(cancellationToken);
        return wishlist;
    }

    public async Task<bool> DeleteWishlistAsync(int id, CancellationToken cancellationToken = default)
    {
        var owned = await LoadOwnedAsync(id, cancellationToken);
        if (owned is null) return false;

        _db.Wishlists.Remove(owned);
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task AddItemAsync(int wishlistId, string title, string? url, string? notes, decimal? price, byte[]? photoData = null, CancellationToken cancellationToken = default)
    {
        var owned = await LoadOwnedAsync(wishlistId, cancellationToken)
            ?? throw new InvalidOperationException("Wunschzettel nicht gefunden oder keine Berechtigung.");

        var next = await _db.WishlistItems.Where(x => x.WishlistId == wishlistId)
            .Select(x => (int?)x.SortOrder).MaxAsync(cancellationToken) ?? -1;

        _db.WishlistItems.Add(new WishlistItem
        {
            WishlistId = wishlistId,
            Title = title.Trim(),
            Url = string.IsNullOrWhiteSpace(url) ? null : url.Trim(),
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
            Price = price,
            PhotoData = photoData is { Length: > 0 } ? photoData : null,
            SortOrder = next + 1,
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task SetItemFulfilledAsync(int itemId, bool fulfilled, CancellationToken cancellationToken = default)
    {
        var item = await LoadOwnedItemAsync(itemId, cancellationToken);
        if (item is null) return;
        item.IsFulfilled = fulfilled;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteItemAsync(int itemId, CancellationToken cancellationToken = default)
    {
        var item = await LoadOwnedItemAsync(itemId, cancellationToken);
        if (item is null) return;
        _db.WishlistItems.Remove(item);
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task<Wishlist?> LoadOwnedAsync(int wishlistId, CancellationToken cancellationToken)
    {
        var householdId = await _currentUser.RequireHouseholdIdAsync();
        var userId = await _currentUser.GetUserIdAsync();
        return await _db.Wishlists.FirstOrDefaultAsync(
            x => x.Id == wishlistId && x.HouseholdId == householdId && x.OwnerUserId == userId, cancellationToken);
    }

    private async Task<WishlistItem?> LoadOwnedItemAsync(int itemId, CancellationToken cancellationToken)
    {
        var householdId = await _currentUser.RequireHouseholdIdAsync();
        var userId = await _currentUser.GetUserIdAsync();
        return await _db.WishlistItems.FirstOrDefaultAsync(
            x => x.Id == itemId && x.Wishlist!.HouseholdId == householdId && x.Wishlist.OwnerUserId == userId, cancellationToken);
    }
}
