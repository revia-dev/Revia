using Microsoft.EntityFrameworkCore;
using PrivateLibrary.Data;
using PrivateLibrary.Modules.Lending.Models;

namespace PrivateLibrary.Modules.Lending.Services;

public sealed class LendingService : ILendingService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;

    public LendingService(IDbContextFactory<AppDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<IReadOnlyList<LendingEntry>> GetAllAsync(LendingEntryStatus? statusFilter = null, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var items = await db.LendingEntries
            .AsNoTracking()
            .Include(x => x.Lender)
            .Include(x => x.Borrower)
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
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var entry = await db.LendingEntries
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
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        entry.CreatedAt = DateTime.UtcNow;
        entry.UpdatedAt = DateTime.UtcNow;
        entry.Status = CalculateStatus(entry);

        db.LendingEntries.Add(entry);
        await db.SaveChangesAsync(cancellationToken);
        return entry;
    }

    public async Task<LendingEntry?> UpdateAsync(LendingEntry entry, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var existing = await db.LendingEntries.FirstOrDefaultAsync(x => x.Id == entry.Id, cancellationToken);
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

        await db.SaveChangesAsync(cancellationToken);
        return existing;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var entry = await db.LendingEntries.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entry is null)
        {
            return false;
        }

        db.LendingEntries.Remove(entry);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<LendingDashboardStats> GetDashboardStatsAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var allEntries = await db.LendingEntries
            .AsNoTracking()
            .Include(x => x.Lender)
            .Include(x => x.Borrower)
            .OrderByDescending(x => x.LoanDate)
            .ToListAsync(cancellationToken);

        foreach (var e in allEntries)
            e.Status = CalculateStatus(e);

        var totalPersons = await db.People.CountAsync(cancellationToken);

        return new LendingDashboardStats
        {
            TotalActive   = allEntries.Count(e => e.Status == LendingEntryStatus.Active),
            TotalOverdue  = allEntries.Count(e => e.Status == LendingEntryStatus.Overdue),
            TotalReturned = allEntries.Count(e => e.Status == LendingEntryStatus.Returned),
            TotalPersons  = totalPersons,
            RecentEntries = allEntries.Take(5).ToList(),
            OverdueEntries = allEntries.Where(e => e.Status == LendingEntryStatus.Overdue).ToList()
        };
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
