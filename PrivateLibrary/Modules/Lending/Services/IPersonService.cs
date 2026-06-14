using PrivateLibrary.Data;

namespace PrivateLibrary.Modules.Lending.Services;

public interface IPersonService
{
    Task<IReadOnlyList<ApplicationUser>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<ApplicationUser?> GetByIdAsync(string id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ApplicationUser>> GetLookupAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> GetAvailableRolesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> GetUserRolesAsync(string userId, CancellationToken cancellationToken = default);
    Task<ApplicationUser> CreateAsync(ApplicationUser user, string password, string roleName, CancellationToken cancellationToken = default);
    Task<PersonOperationResult> SetPasswordAsync(string userId, string newPassword, CancellationToken cancellationToken = default);
    Task<ApplicationUser?> UpdateAsync(ApplicationUser user, string? roleName = null, CancellationToken cancellationToken = default);
    Task<PersonOperationResult> DeleteAsync(string id, CancellationToken cancellationToken = default);
    Task<PersonOperationResult> CreateRoleAsync(string roleName, CancellationToken cancellationToken = default);
    Task<PersonOperationResult> DeleteRoleAsync(string roleName, CancellationToken cancellationToken = default);
}

public sealed record PersonOperationResult(bool Succeeded, string? ErrorMessage = null);
