using PrivateLibrary.Modules.Lending.Models;

namespace PrivateLibrary.Modules.Lending.Services;

public interface ILendingService
{
    Task<IReadOnlyList<LendingEntry>> GetAllAsync(LendingEntryStatus? statusFilter = null, CancellationToken cancellationToken = default);
    Task<LendingEntry?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<LendingEntry> CreateAsync(LendingEntry entry, CancellationToken cancellationToken = default);
    Task<LendingEntry?> UpdateAsync(LendingEntry entry, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);
    Task<LendingPhoto> AddPhotoAsync(int lendingEntryId, Stream content, string contentType, string? originalName, CancellationToken cancellationToken = default);
    Task<bool> RemovePhotoAsync(int photoId, CancellationToken cancellationToken = default);
    Task<LendingDashboardStats> GetDashboardStatsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LendingCategory>> GetCategoriesAsync(CancellationToken cancellationToken = default);
    Task<LendingCategory?> GetCategoryByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<LendingCategory> GetOrCreateCategoryAsync(string name, CancellationToken cancellationToken = default);
    Task<LendingCategory> CreateCategoryAsync(LendingCategory category, CancellationToken cancellationToken = default);
    Task<LendingCategory?> UpdateCategoryAsync(LendingCategory category, CancellationToken cancellationToken = default);
    Task<bool> DeleteCategoryAsync(int id, CancellationToken cancellationToken = default);
    LendingEntryStatus CalculateStatus(LendingEntry entry);
    void ApplyCalculatedStatus(LendingEntry entry);
}
