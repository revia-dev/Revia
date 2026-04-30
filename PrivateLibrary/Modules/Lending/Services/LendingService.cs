using Microsoft.EntityFrameworkCore;
using PrivateLibrary.Data;
using PrivateLibrary.Features.Lending.Models;

namespace PrivateLibrary.Features.Lending.Services;

public sealed class LendingService : ILendingService
{
    private readonly AppDbContext _db;

    public LendingService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<LendingEntry>> GetAllAsync(LendingEntryStatus? statusFilter = null, CancellationToken cancellationToken = default)
    {
        var query = _db.LendingEntries
            .AsNoTracking()
            .Include(x => x.Lender)
            .Include(x => x.Borrower)
            .AsQueryable();

        var items = await query
            .OrderByDescending(x => x.LoanDate)
            .ThenByDescending(x => x.Id)
            .ToListAsync(cancellationToken);

        foreach (var item in items)
        {
            item.Status = CalculateStatus(item);
        }

        if (statusFilter.HasValue)
        {
            items = items.Where(x => CalculateStatus(x) == statusFilter.Value).ToList();
        }

        return items;
    }

    public async Task<LendingEntry?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var entry = await _db.LendingEntries
            .Include(x => x.Lender)
            .Include(x => x.Borrower)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (entry is not null)
        {
            entry.Status = CalculateStatus(entry);
        }

        return entry;
    }

    public async Task<LendingEntry> CreateAsync(LendingEntry entry, CancellationToken cancellationToken = default)
    {
        entry.CreatedAt = DateTime.UtcNow;
        entry.UpdatedAt = DateTime.UtcNow;
        entry.Status = CalculateStatus(entry);

        _db.LendingEntries.Add(entry);
        await _db.SaveChangesAsync(cancellationToken);
        return entry;
    }

    public async Task<LendingEntry?> UpdateAsync(LendingEntry entry, CancellationToken cancellationToken = default)
    {
        var existing = await _db.LendingEntries.FirstOrDefaultAsync(x => x.Id == entry.Id, cancellationToken);
        if (existing is null)
        {
            return null;
        }

        existing.Title = entry.Title;
        existing.Description = entry.Description;
        existing.ItemName = entry.ItemName;
        existing.Category = entry.Category;
        existing.LenderId = entry.LenderId;
        existing.BorrowerId = entry.BorrowerId;
        existing.LoanDate = entry.LoanDate;
        existing.DueDate = entry.DueDate;
        existing.ReturnedAt = entry.ReturnedAt;
        existing.Notes = entry.Notes;
        existing.Status = CalculateStatus(entry);
        existing.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
        return existing;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var entry = await _db.LendingEntries.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entry is null)
        {
            return false;
        }

        _db.LendingEntries.Remove(entry);
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public LendingEntryStatus CalculateStatus(LendingEntry entry)
    {
        if (entry.ReturnedAt.HasValue)
        {
            return LendingEntryStatus.Returned;
        }

        if (entry.Status == LendingEntryStatus.Lost)
        {
            return LendingEntryStatus.Lost;
        }

        if (entry.Status == LendingEntryStatus.GivenAway)
        {
            return LendingEntryStatus.GivenAway;
        }

        if (entry.DueDate.HasValue && entry.DueDate.Value.Date < DateTime.Today)
        {
            return LendingEntryStatus.Overdue;
        }

        return LendingEntryStatus.Active;
    }

    public void ApplyCalculatedStatus(LendingEntry entry)
    {
        entry.Status = CalculateStatus(entry);
    }
}
