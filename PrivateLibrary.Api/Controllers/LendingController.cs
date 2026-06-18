using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrivateLibrary.Api.Auth;
using PrivateLibrary.Services;
using PrivateLibrary.Data;
using PrivateLibrary.Modules.Lending.Models;
using PrivateLibrary.Modules.Lending.Services;
using PrivateLibrary.Shared.Lending;
using PrivateLibrary.Shared.Media;

namespace PrivateLibrary.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/lending")]
[ModuleAccess(AppModules.Lending)]
public sealed class LendingController(ILendingService lending) : ControllerBase
{
    private readonly ILendingService _lending = lending;

    [HttpGet]
    public async Task<ActionResult<List<LendingEntryDto>>> GetAll(CancellationToken ct)
        => (await _lending.GetAllAsync(cancellationToken: ct)).Select(ToDto).ToList();

    [HttpGet("{id:int}")]
    public async Task<ActionResult<LendingEntryDto>> GetById(int id, CancellationToken ct)
    {
        var entry = await _lending.GetByIdAsync(id, ct);
        return entry is null ? NotFound() : ToDto(entry);
    }

    [HttpPost]
    public async Task<ActionResult<LendingEntryDto>> Create([FromBody] SaveLendingEntryRequest request, CancellationToken ct)
    {
        var created = await _lending.CreateAsync(ToEntity(request), ct);
        var full = await _lending.GetByIdAsync(created.Id, ct);
        return ToDto(full ?? created);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<LendingEntryDto>> Update(int id, [FromBody] SaveLendingEntryRequest request, CancellationToken ct)
    {
        request.Id = id;
        var updated = await _lending.UpdateAsync(ToEntity(request), ct);
        if (updated is null) { return NotFound(); }
        var full = await _lending.GetByIdAsync(id, ct);
        return ToDto(full ?? updated);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
        => await _lending.DeleteAsync(id, ct) ? NoContent() : NotFound();

    /// <summary>Quick action: mark an entry returned today.</summary>
    [HttpPut("{id:int}/returned")]
    public async Task<ActionResult<LendingEntryDto>> MarkReturned(int id, CancellationToken ct)
    {
        var entry = await _lending.GetByIdAsync(id, ct);
        if (entry is null) { return NotFound(); }

        var save = new SaveLendingEntryRequest
        {
            Id = entry.Id,
            Title = entry.Title,
            Description = entry.Description,
            Items = entry.Items.OrderBy(i => i.SortOrder).Select(i => i.Name).ToList(),
            CategoryIds = entry.CategoryLinks.Select(c => c.LendingCategoryId).ToList(),
            LenderId = entry.LenderId,
            BorrowerId = entry.BorrowerId,
            LoanDate = entry.LoanDate,
            DueDate = entry.DueDate,
            ReturnedAt = DateTime.Today,
            Notes = entry.Notes
        };

        var updated = await _lending.UpdateAsync(ToEntity(save), ct);
        var full = await _lending.GetByIdAsync(id, ct);
        return ToDto(full ?? updated!);
    }

    // ---- categories ----

    [HttpGet("categories")]
    public async Task<ActionResult<List<LendingCategoryDto>>> GetCategories(CancellationToken ct)
        => (await _lending.GetCategoriesAsync(ct))
            .Select(c => new LendingCategoryDto { Id = c.Id, Name = c.Name })
            .ToList();

    [HttpPost("categories")]
    public async Task<ActionResult<LendingCategoryDto>> CreateCategory([FromBody] CreateLendingCategoryRequest request, CancellationToken ct)
    {
        var created = await _lending.GetOrCreateCategoryAsync(request.Name, ct);
        return new LendingCategoryDto { Id = created.Id, Name = created.Name };
    }

    [HttpDelete("categories/{id:int}")]
    public async Task<IActionResult> DeleteCategory(int id, CancellationToken ct)
        => await _lending.DeleteCategoryAsync(id, ct) ? NoContent() : NotFound();

    // ---- photos ----

    /// <summary>Streams a lending photo. Authenticated; lending is shared across the
    /// linked households, so visibility follows the same model as the entries.</summary>
    [HttpGet("photos/{id:int}")]
    public async Task<IActionResult> Photo(
        int id,
        [FromServices] AppDbContext db,
        [FromServices] ILendingPhotoStorage storage,
        CancellationToken ct)
    {
        var photo = await db.LendingPhotos.AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new { x.FileName, x.ContentType })
            .FirstOrDefaultAsync(ct);

        if (photo is null) return NotFound();
        var path = storage.GetPath(photo.FileName);
        if (!System.IO.File.Exists(path)) return NotFound();
        return PhysicalFile(path, photo.ContentType);
    }

    /// <summary>Attaches a photo (base64) to an existing lending entry.</summary>
    [HttpPost("{id:int}/photos")]
    public async Task<IActionResult> AddPhoto(int id, [FromBody] UploadPhotoRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Base64)) return BadRequest("Kein Foto.");
        byte[] bytes;
        try { bytes = Convert.FromBase64String(request.Base64); }
        catch (FormatException) { return BadRequest("Ungültiges Foto."); }

        using var stream = new MemoryStream(bytes);
        await _lending.AddPhotoAsync(id, stream, request.ContentType, request.FileName, ct);
        return NoContent();
    }

    /// <summary>Removes a lending photo.</summary>
    [HttpDelete("photos/{photoId:int}")]
    public async Task<IActionResult> RemovePhoto(int photoId, CancellationToken ct)
        => await _lending.RemovePhotoAsync(photoId, ct) ? NoContent() : NotFound();

    // ---- mapping ----

    private static LendingEntry ToEntity(SaveLendingEntryRequest r) => new()
    {
        Id = r.Id,
        Title = r.Title,
        Description = r.Description,
        LenderId = r.LenderId,
        BorrowerId = r.BorrowerId,
        LoanDate = r.LoanDate == default ? DateTime.Today : r.LoanDate,
        DueDate = r.DueDate,
        ReturnedAt = r.ReturnedAt,
        Notes = r.Notes,
        Items = r.Items
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Select((n, i) => new LendingItem { Name = n.Trim(), SortOrder = i })
            .ToList(),
        CategoryLinks = r.CategoryIds
            .Distinct()
            .Select(cid => new LendingEntryCategory { LendingCategoryId = cid })
            .ToList()
    };

    private static LendingEntryDto ToDto(LendingEntry e) => new()
    {
        Id = e.Id,
        Title = e.Title,
        Description = e.Description,
        Items = e.Items.OrderBy(i => i.SortOrder).Select(i => i.Name).ToList(),
        CategoryIds = e.CategoryLinks.Select(c => c.LendingCategoryId).ToList(),
        CategoryNames = e.CategoryLinks
            .Where(c => c.LendingCategory is not null)
            .Select(c => c.LendingCategory!.Name)
            .ToList(),
        LenderId = e.LenderId,
        LenderName = e.Lender?.DisplayName ?? string.Empty,
        BorrowerId = e.BorrowerId,
        BorrowerName = e.Borrower?.DisplayName ?? string.Empty,
        LoanDate = e.LoanDate,
        DueDate = e.DueDate,
        ReturnedAt = e.ReturnedAt,
        Status = (LendingStatusDto)(int)e.Status,
        Notes = e.Notes,
        PhotoUrls = (e.Photos ?? new List<LendingPhoto>())
            .OrderBy(p => p.SortOrder).ThenBy(p => p.Id)
            .Select(p => $"api/lending/photos/{p.Id}")
            .ToList()
    };
}
