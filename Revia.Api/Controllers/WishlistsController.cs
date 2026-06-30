using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Revia.Api.Auth;
using Revia.Data;
using Revia.Modules.Wishlists;
using Revia.Services;
using Revia.Shared.Wishlists;

namespace Revia.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/wishlists")]
[ModuleAccess(AppModules.Wishlists)]
public sealed class WishlistsController(
    WishlistService wishlists,
    CurrentUserContext currentUser) : ControllerBase
{
    private readonly WishlistService _wishlists = wishlists;
    private readonly CurrentUserContext _currentUser = currentUser;

    [HttpGet]
    public async Task<ActionResult<List<WishlistDto>>> GetAll(CancellationToken ct)
    {
        var userId = await _currentUser.GetUserIdAsync();
        var lists = await _wishlists.GetHouseholdWishlistsAsync(ct);
        return lists.Select(w => ToDto(w, userId)).ToList();
    }

    [HttpPost]
    public async Task<ActionResult<WishlistDto>> Create([FromBody] CreateWishlistRequest request, CancellationToken ct)
    {
        var created = await _wishlists.CreateWishlistAsync(request.Name, ct);
        var full = await _wishlists.GetWishlistAsync(created.Id, ct);
        var userId = await _currentUser.GetUserIdAsync();
        return ToDto(full!, userId);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var ok = await _wishlists.DeleteWishlistAsync(id, ct);
        return ok ? NoContent() : NotFound();
    }

    // ---- items ----

    [HttpPost("items")]
    public async Task<IActionResult> AddItem([FromBody] AddWishlistItemRequest request, CancellationToken ct)
    {
        await _wishlists.AddItemAsync(request.WishlistId, request.Title, request.Url, request.Notes, request.Price, request.PhotoData, ct);
        return NoContent();
    }

    [HttpPut("items/{itemId:int}/fulfilled")]
    public async Task<IActionResult> SetFulfilled(int itemId, [FromBody] SetItemFulfilledRequest request, CancellationToken ct)
    {
        await _wishlists.SetItemFulfilledAsync(itemId, request.Fulfilled, ct);
        return NoContent();
    }

    [HttpDelete("items/{itemId:int}")]
    public async Task<IActionResult> DeleteItem(int itemId, CancellationToken ct)
    {
        await _wishlists.DeleteItemAsync(itemId, ct);
        return NoContent();
    }

    private static WishlistDto ToDto(Wishlist w, string? currentUserId) => new()
    {
        Id = w.Id,
        Name = w.Name,
        OwnerUserId = w.OwnerUserId,
        OwnerDisplayName = w.OwnerUser?.DisplayName ?? string.Empty,
        CanEdit = currentUserId is not null && w.OwnerUserId == currentUserId,
        Items = w.Items
            .OrderBy(i => i.SortOrder)
            .Select(i => new WishlistItemDto
            {
                Id = i.Id,
                Title = i.Title,
                Url = i.Url,
                Notes = i.Notes,
                Price = i.Price,
                IsFulfilled = i.IsFulfilled,
                PhotoData = i.PhotoData
            }).ToList()
    };
}
