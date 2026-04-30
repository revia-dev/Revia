using PrivateLibrary.Features.Lending.Models;

namespace PrivateLibrary.Features.Lending.Services;

public interface ILendingService
{
    Task<IReadOnlyList<LendingEntry>> GetAllAsync(LendingEntryStatus? statusFilter = null, CancellationToken cancellationToken = default);
    Task<LendingEntry?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<LendingEntry> CreateAsync(LendingEntry entry, CancellationToken cancellationToken = default);
    Task<LendingEntry?> UpdateAsync(LendingEntry entry, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);
    LendingEntryStatus CalculateStatus(LendingEntry entry);
    void ApplyCalculatedStatus(LendingEntry entry);
}
