using Microsoft.EntityFrameworkCore;
using PrivateLibrary.Data;
using PrivateLibrary.Modules.Lending.Models;

namespace PrivateLibrary.Modules.Lending.Services;

public sealed class LendingService : ILendingService
{
    private readonly AppDbContext _db;

    public LendingService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<LendingEntry>> GetAllAsync(LendingEntryStatus? statusFilter = null, CancellationToken cancellationToken = default)
    {
        var items = await _db.LendingEntries
            .AsNoTracking()
            .Include(x => x.Lender)
            .Include(x => x.Borrower)
            .Include(x => x.Items.OrderBy(i => i.SortOrder))
            .Include(x => x.CategoryLinks)
                .ThenInclude(x => x.LendingCategory)
            .OrderByDescending(x => x.LoanDate)
            .ThenByDescending(x => x.Id)
            .ToListAsync(cancellationToken);

        foreach (var item in items)
        {
            NormalizeLegacyFields(item);
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
            .Include(x => x.Items.OrderBy(i => i.SortOrder))
            .Include(x => x.CategoryLinks)
                .ThenInclude(x => x.LendingCategory)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (entry is not null)
        {
            NormalizeLegacyFields(entry);
            entry.Status = CalculateStatus(entry);
        }

        return entry;
    }

    public async Task<LendingEntry> CreateAsync(LendingEntry entry, CancellationToken cancellationToken = default)
    {
        await AttachCategoriesAsync(_db, entry, cancellationToken);
        NormalizeLegacyFields(entry);
        entry.CreatedAt = DateTime.UtcNow;
        entry.UpdatedAt = DateTime.UtcNow;
        entry.Status = CalculateStatus(entry);

        _db.LendingEntries.Add(entry);
        await _db.SaveChangesAsync(cancellationToken);
        return entry;
    }

    public async Task<LendingEntry?> UpdateAsync(LendingEntry entry, CancellationToken cancellationToken = default)
    {
        var existing = await _db.LendingEntries
            .Include(x => x.Items)
            .Include(x => x.CategoryLinks)
            .FirstOrDefaultAsync(x => x.Id == entry.Id, cancellationToken);

        if (existing is null)
        {
            return null;
        }

        existing.Title = entry.Title;
        existing.Description = entry.Description;
        existing.LenderId = entry.LenderId;
        existing.BorrowerId = entry.BorrowerId;
        existing.LoanDate = entry.LoanDate;
        existing.DueDate = entry.DueDate;
        existing.ReturnedAt = entry.ReturnedAt;
        existing.Notes = entry.Notes;

        existing.Items.Clear();
        foreach (var item in entry.Items
                     .Where(x => !string.IsNullOrWhiteSpace(x.Name))
                     .OrderBy(x => x.SortOrder))
        {
            existing.Items.Add(new LendingItem
            {
                Name = item.Name.Trim(),
                SortOrder = item.SortOrder
            });
        }

        existing.CategoryLinks.Clear();
        foreach (var link in entry.CategoryLinks)
        {
            existing.CategoryLinks.Add(new LendingEntryCategory
            {
                LendingCategoryId = link.LendingCategoryId
            });
        }

        await AttachCategoriesAsync(_db, existing, cancellationToken);
        NormalizeLegacyFields(existing);
        existing.Status = CalculateStatus(existing);
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

    public async Task<LendingDashboardStats> GetDashboardStatsAsync(CancellationToken cancellationToken = default)
    {
        var allEntries = await _db.LendingEntries
            .AsNoTracking()
            .Include(x => x.Lender)
            .Include(x => x.Borrower)
            .Include(x => x.Items.OrderBy(i => i.SortOrder))
            .Include(x => x.CategoryLinks)
                .ThenInclude(x => x.LendingCategory)
            .OrderByDescending(x => x.LoanDate)
            .ToListAsync(cancellationToken);

        foreach (var e in allEntries)
        {
            NormalizeLegacyFields(e);
            e.Status = CalculateStatus(e);
        }

        var totalPersons = await _db.People.CountAsync(cancellationToken);

        return new LendingDashboardStats
        {
            TotalActive = allEntries.Count(e => e.Status == LendingEntryStatus.Active),
            TotalOverdue = allEntries.Count(e => e.Status == LendingEntryStatus.Overdue),
            TotalReturned = allEntries.Count(e => e.Status == LendingEntryStatus.Returned),
            TotalPersons = totalPersons,
            RecentEntries = allEntries.Take(5).ToList(),
            OverdueEntries = allEntries.Where(e => e.Status == LendingEntryStatus.Overdue).ToList()
        };
    }

    public async Task<IReadOnlyList<LendingCategory>> GetCategoriesAsync(CancellationToken cancellationToken = default)
    {
        return await _db.LendingCategories
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<LendingCategory?> GetCategoryByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _db.LendingCategories
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<LendingCategory> GetOrCreateCategoryAsync(string name, CancellationToken cancellationToken = default)
    {
        var normalizedName = name.Trim();

        var existing = await _db.LendingCategories
            .FirstOrDefaultAsync(x => x.Name.ToLower() == normalizedName.ToLower(), cancellationToken);

        if (existing is not null)
        {
            return existing;
        }

        var category = new LendingCategory
        {
            Name = normalizedName,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.LendingCategories.Add(category);
        await _db.SaveChangesAsync(cancellationToken);
        return category;
    }

    public async Task<LendingCategory> CreateCategoryAsync(LendingCategory category, CancellationToken cancellationToken = default)
    {
        var normalizedName = (category.Name ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            throw new InvalidOperationException("Der Kategoriename darf nicht leer sein.");
        }

        var existing = await _db.LendingCategories
            .FirstOrDefaultAsync(x => x.Name.ToLower() == normalizedName.ToLower(), cancellationToken);

        if (existing is not null)
        {
            return existing;
        }

        category.Name = normalizedName;
        category.CreatedAt = DateTime.UtcNow;
        category.UpdatedAt = DateTime.UtcNow;

        _db.LendingCategories.Add(category);
        await _db.SaveChangesAsync(cancellationToken);
        return category;
    }

    public async Task<LendingCategory?> UpdateCategoryAsync(LendingCategory category, CancellationToken cancellationToken = default)
    {
        var existing = await _db.LendingCategories.FirstOrDefaultAsync(x => x.Id == category.Id, cancellationToken);
        if (existing is null)
        {
            return null;
        }

        var normalizedName = (category.Name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            throw new InvalidOperationException("Der Kategoriename darf nicht leer sein.");
        }

        var duplicateExists = await _db.LendingCategories
            .AnyAsync(x => x.Id != category.Id && x.Name.ToLower() == normalizedName.ToLower(), cancellationToken);
        if (duplicateExists)
        {
            throw new InvalidOperationException("Eine Kategorie mit diesem Namen existiert bereits.");
        }

        existing.Name = normalizedName;
        existing.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
        return existing;
    }

    public async Task<bool> DeleteCategoryAsync(int id, CancellationToken cancellationToken = default)
    {
        var category = await _db.LendingCategories
            .Include(x => x.EntryLinks)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (category is null)
        {
            return false;
        }

        if (category.EntryLinks.Any())
        {
            throw new InvalidOperationException("Die Kategorie wird noch von Ausleihen verwendet und kann nicht gelöscht werden.");
        }

        _db.LendingCategories.Remove(category);
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

    private static void NormalizeLegacyFields(LendingEntry entry)
    {
        var orderedItems = entry.Items
            .Where(x => !string.IsNullOrWhiteSpace(x.Name))
            .OrderBy(x => x.SortOrder)
            .ToList();

        entry.ItemName = orderedItems.FirstOrDefault()?.Name ?? string.Empty;
        entry.Category = string.Join(", ", entry.CategoryLinks
            .Select(x => x.LendingCategory?.Name)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct());
    }

    private async Task AttachCategoriesAsync(AppDbContext db, LendingEntry entry, CancellationToken cancellationToken)
    {
        var categoryIds = entry.CategoryLinks
            .Select(x => x.LendingCategoryId)
            .Where(x => x > 0)
            .Distinct()
            .ToList();

        entry.CategoryLinks.Clear();

        foreach (var categoryId in categoryIds)
        {
            var exists = await db.LendingCategories.AnyAsync(x => x.Id == categoryId, cancellationToken);
            if (!exists)
            {
                continue;
            }

            entry.CategoryLinks.Add(new LendingEntryCategory
            {
                LendingCategoryId = categoryId
            });
        }
    }
}
