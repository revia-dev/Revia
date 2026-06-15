using PrivateLibrary.Data;

namespace PrivateLibrary.Modules.Lending.Services;

public interface IPersonService
{
    Task<IReadOnlyList<ApplicationUser>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<ApplicationUser?> GetByIdAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>Finds a person by their QR link token across all households (for import).</summary>
    Task<ApplicationUser?> GetByLinkTokenAsync(Guid token, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ApplicationUser>> GetLookupAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> GetAvailableRolesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> GetUserRolesAsync(string userId, CancellationToken cancellationToken = default);
    Task<ApplicationUser> CreateAsync(ApplicationUser user, string password, string roleName, CancellationToken cancellationToken = default);

    /// <summary>Creates a pure data person without password/login (cannot sign in).</summary>
    Task<ApplicationUser> CreateLoginlessAsync(ApplicationUser user, CancellationToken cancellationToken = default);

    Task<PersonOperationResult> SetPasswordAsync(string userId, string newPassword, CancellationToken cancellationToken = default);
    Task<ApplicationUser?> UpdateAsync(ApplicationUser user, string? roleName = null, CancellationToken cancellationToken = default);

    /// <summary>Replaces the person's single profile photo, deleting any previous one.</summary>
    Task<PersonOperationResult> SetProfilePhotoAsync(string userId, Stream content, string contentType, string? originalName, CancellationToken cancellationToken = default);
    Task<PersonOperationResult> RemoveProfilePhotoAsync(string userId, CancellationToken cancellationToken = default);
    Task<PersonOperationResult> DeleteAsync(string id, CancellationToken cancellationToken = default);
    Task<PersonOperationResult> CreateRoleAsync(string roleName, CancellationToken cancellationToken = default);
    Task<PersonOperationResult> DeleteRoleAsync(string roleName, CancellationToken cancellationToken = default);
}

public sealed record PersonOperationResult(bool Succeeded, string? ErrorMessage = null);
