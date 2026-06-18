using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PrivateLibrary.Data;
using PrivateLibrary.Modules.Lending.Models;
using PrivateLibrary.Modules.Lending.Services;
using PrivateLibrary.Shared.Lending;

namespace PrivateLibrary.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/lending")]
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
        Notes = e.Notes
    };
}
