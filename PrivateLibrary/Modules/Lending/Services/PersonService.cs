using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PrivateLibrary.Data;
using PrivateLibrary.Services;

namespace PrivateLibrary.Modules.Lending.Services;

public sealed class PersonService(
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole> roleManager,
    CurrentUserContext currentUser,
    IPersonPhotoStorage photoStorage) : IPersonService
{
    private readonly UserManager<ApplicationUser> _userManager = userManager;
    private readonly RoleManager<IdentityRole> _roleManager = roleManager;
    private readonly CurrentUserContext _currentUser = currentUser;
    private readonly IPersonPhotoStorage _photoStorage = photoStorage;

    public async Task<IReadOnlyList<ApplicationUser>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var householdId = await _currentUser.RequireHouseholdIdAsync();
        return await _userManager.Users
            .AsNoTracking()
            .Where(x => x.HouseholdId == householdId)
            .OrderBy(x => x.LastName)
            .ThenBy(x => x.FirstName)
            .ThenBy(x => x.Nickname)
            .ToListAsync(cancellationToken);
    }

    public async Task<ApplicationUser?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        var householdId = await _currentUser.RequireHouseholdIdAsync();
        return await _userManager.Users.FirstOrDefaultAsync(x => x.Id == id && x.HouseholdId == householdId, cancellationToken);
    }

    public async Task<IReadOnlyList<ApplicationUser>> GetLookupAsync(CancellationToken cancellationToken = default)
    {
        var householdId = await _currentUser.RequireHouseholdIdAsync();
        return await _userManager.Users
            .AsNoTracking()
            .Where(x => x.HouseholdId == householdId)
            .OrderBy(x => x.LastName)
            .ThenBy(x => x.FirstName)
            .Select(x => new ApplicationUser
            {
                Id = x.Id,
                FirstName = x.FirstName,
                LastName = x.LastName,
                Nickname = x.Nickname,
                Email = x.Email,
                UserName = x.UserName,
                IsSystemUser = x.IsSystemUser,
                IsLoginless = x.IsLoginless,
                ProfilePhotoFileName = x.ProfilePhotoFileName
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<string>> GetAvailableRolesAsync(CancellationToken cancellationToken = default)
    {
        return await _roleManager.Roles
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(x => x.Name!)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<string>> GetUserRolesAsync(string userId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null) return [];
        var roles = await _userManager.GetRolesAsync(user);
        return [.. roles.OrderBy(r => r)];
    }

    public async Task<ApplicationUser> CreateAsync(ApplicationUser user, string password, string roleName, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        user.HouseholdId ??= await _currentUser.RequireHouseholdIdAsync();
        user.UserName = string.IsNullOrWhiteSpace(user.UserName)
            ? user.Email
            : user.UserName;
        user.Email = user.Email?.Trim();
        user.EmailConfirmed = true;
        // Do NOT set NormalizedEmail/NormalizedUserName manually —
        // UserManager.CreateAsync handles normalization internally.

        var result = await _userManager.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(string.Join(", ", result.Errors.Select(x => x.Description)));
        }

        var resolvedRole = string.IsNullOrWhiteSpace(roleName)
            ? IdentitySeeder.UserRoleName
            : roleName;

        if (!await _roleManager.RoleExistsAsync(resolvedRole))
        {
            await _roleManager.CreateAsync(new IdentityRole(resolvedRole));
        }

        var addRoleResult = await _userManager.AddToRoleAsync(user, resolvedRole);
        if (!addRoleResult.Succeeded)
        {
            throw new InvalidOperationException(string.Join(", ", addRoleResult.Errors.Select(x => x.Description)));
        }

        return user;
    }

    public async Task<ApplicationUser> CreateLoginlessAsync(ApplicationUser user, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        user.HouseholdId ??= await _currentUser.RequireHouseholdIdAsync();
        user.IsLoginless = true;
        user.EmailConfirmed = true;
        user.Email = string.IsNullOrWhiteSpace(user.Email) ? null : user.Email.Trim();

        // A loginless person still needs a unique UserName for Identity, but no password.
        user.UserName = string.IsNullOrWhiteSpace(user.UserName)
            ? $"person-{Guid.NewGuid():N}"
            : user.UserName.Trim();

        var result = await _userManager.CreateAsync(user);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(string.Join(", ", result.Errors.Select(x => x.Description)));
        }

        return user;
    }

    public async Task<PersonOperationResult> SetProfilePhotoAsync(string userId, Stream content, string contentType, string? originalName, CancellationToken cancellationToken = default)
    {
        var householdId = await _currentUser.RequireHouseholdIdAsync();
        var user = await _userManager.Users.FirstOrDefaultAsync(x => x.Id == userId && x.HouseholdId == householdId, cancellationToken);
        if (user is null)
        {
            return new PersonOperationResult(false, "Person wurde nicht gefunden.");
        }

        var extension = ExtensionForContentType(contentType, originalName);
        var fileName = await _photoStorage.SaveAsync(content, extension, cancellationToken);
        var previous = user.ProfilePhotoFileName;

        user.ProfilePhotoFileName = fileName;
        var updateResult = await _userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            _photoStorage.Delete(fileName);
            return new PersonOperationResult(false, string.Join(", ", updateResult.Errors.Select(x => x.Description)));
        }

        if (!string.IsNullOrWhiteSpace(previous))
        {
            _photoStorage.Delete(previous);
        }

        return new PersonOperationResult(true);
    }

    public async Task<PersonOperationResult> RemoveProfilePhotoAsync(string userId, CancellationToken cancellationToken = default)
    {
        var householdId = await _currentUser.RequireHouseholdIdAsync();
        var user = await _userManager.Users.FirstOrDefaultAsync(x => x.Id == userId && x.HouseholdId == householdId, cancellationToken);
        if (user is null)
        {
            return new PersonOperationResult(false, "Person wurde nicht gefunden.");
        }

        var previous = user.ProfilePhotoFileName;
        if (string.IsNullOrWhiteSpace(previous))
        {
            return new PersonOperationResult(true);
        }

        user.ProfilePhotoFileName = null;
        var updateResult = await _userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            return new PersonOperationResult(false, string.Join(", ", updateResult.Errors.Select(x => x.Description)));
        }

        _photoStorage.Delete(previous);
        return new PersonOperationResult(true);
    }

    private static string ExtensionForContentType(string contentType, string? originalName)
    {
        var ext = contentType?.ToLowerInvariant() switch
        {
            "image/jpeg" or "image/jpg" => ".jpg",
            "image/png" => ".png",
            "image/webp" => ".webp",
            "image/gif" => ".gif",
            "image/heic" => ".heic",
            "image/heif" => ".heif",
            _ => null
        };

        if (ext is not null)
        {
            return ext;
        }

        var fromName = Path.GetExtension(originalName ?? string.Empty);
        return string.IsNullOrWhiteSpace(fromName) ? ".img" : fromName.ToLowerInvariant();
    }

    public async Task<PersonOperationResult> SetPasswordAsync(string userId, string newPassword, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
            return new PersonOperationResult(false, "Benutzer wurde nicht gefunden.");

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var result = await _userManager.ResetPasswordAsync(user, token, newPassword);
        return result.Succeeded
            ? new PersonOperationResult(true)
            : new PersonOperationResult(false, string.Join(", ", result.Errors.Select(x => x.Description)));
    }

    public async Task<ApplicationUser?> UpdateAsync(ApplicationUser user, string? roleName = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var existing = await _userManager.Users.FirstOrDefaultAsync(x => x.Id == user.Id, cancellationToken);
        if (existing is null)
        {
            return null;
        }

        existing.FirstName = user.FirstName;
        existing.LastName = user.LastName;
        existing.Nickname = user.Nickname;
        existing.Email = user.Email?.Trim();
        existing.UserName = string.IsNullOrWhiteSpace(user.UserName) ? user.Email : user.UserName;
        existing.Phone = user.Phone;
        existing.Street = user.Street;
        existing.HouseNumber = user.HouseNumber;
        existing.PostalCode = user.PostalCode;
        existing.City = user.City;
        existing.Notes = user.Notes;

        var updateResult = await _userManager.UpdateAsync(existing);
        if (!updateResult.Succeeded)
        {
            throw new InvalidOperationException(string.Join(", ", updateResult.Errors.Select(x => x.Description)));
        }

        if (!string.IsNullOrWhiteSpace(roleName))
        {
            var currentRoles = await _userManager.GetRolesAsync(existing);
            if (!currentRoles.Contains(roleName, StringComparer.OrdinalIgnoreCase))
            {
                if (existing.UserName == IdentitySeeder.DefaultAdminUserName &&
                    string.Equals(roleName, IdentitySeeder.UserRoleName, StringComparison.OrdinalIgnoreCase))
                {
                    return existing;
                }

                if (currentRoles.Contains(IdentitySeeder.AdminRoleName, StringComparer.OrdinalIgnoreCase) &&
                    !string.Equals(roleName, IdentitySeeder.AdminRoleName, StringComparison.OrdinalIgnoreCase))
                {
                    var adminCount = await CountUsersInRoleAsync(IdentitySeeder.AdminRoleName, cancellationToken);
                    if (adminCount <= 1)
                    {
                        throw new InvalidOperationException("Dem letzten Administrator darf die Admin-Rolle nicht entzogen werden.");
                    }
                }

                if (currentRoles.Count > 0)
                {
                    var removeResult = await _userManager.RemoveFromRolesAsync(existing, currentRoles);
                    if (!removeResult.Succeeded)
                    {
                        throw new InvalidOperationException(string.Join(", ", removeResult.Errors.Select(x => x.Description)));
                    }
                }

                if (!await _roleManager.RoleExistsAsync(roleName))
                {
                    await _roleManager.CreateAsync(new IdentityRole(roleName));
                }

                var addRoleResult = await _userManager.AddToRoleAsync(existing, roleName);
                if (!addRoleResult.Succeeded)
                {
                    throw new InvalidOperationException(string.Join(", ", addRoleResult.Errors.Select(x => x.Description)));
                }
            }
        }

        return existing;
    }

    public async Task<PersonOperationResult> DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var user = await _userManager.Users.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (user is null)
        {
            return new PersonOperationResult(false, "Benutzer wurde nicht gefunden.");
        }

        if (user.UserName == IdentitySeeder.DefaultAdminUserName || user.IsSystemUser)
        {
            return new PersonOperationResult(false, "Der Default-Admin darf nicht gelöscht werden.");
        }

        var isAdmin = await _userManager.IsInRoleAsync(user, IdentitySeeder.AdminRoleName);
        if (isAdmin)
        {
            var adminCount = await CountUsersInRoleAsync(IdentitySeeder.AdminRoleName, cancellationToken);
            if (adminCount <= 1)
            {
                return new PersonOperationResult(false, "Der letzte Administrator darf nicht gelöscht werden.");
            }
        }

        var photoFileName = user.ProfilePhotoFileName;
        var result = await _userManager.DeleteAsync(user);
        if (!result.Succeeded)
        {
            return new PersonOperationResult(false, string.Join(", ", result.Errors.Select(x => x.Description)));
        }

        if (!string.IsNullOrWhiteSpace(photoFileName))
        {
            _photoStorage.Delete(photoFileName);
        }

        return new PersonOperationResult(true);
    }

    public async Task<PersonOperationResult> CreateRoleAsync(string roleName, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var normalizedRoleName = roleName?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedRoleName))
        {
            return new PersonOperationResult(false, "Rollenname darf nicht leer sein.");
        }

        if (await _roleManager.RoleExistsAsync(normalizedRoleName))
        {
            return new PersonOperationResult(false, "Die Rolle existiert bereits.");
        }

        var result = await _roleManager.CreateAsync(new IdentityRole(normalizedRoleName));
        return result.Succeeded
            ? new PersonOperationResult(true)
            : new PersonOperationResult(false, string.Join(", ", result.Errors.Select(x => x.Description)));
    }

    public async Task<PersonOperationResult> DeleteRoleAsync(string roleName, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var normalizedRoleName = roleName?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedRoleName))
        {
            return new PersonOperationResult(false, "Rollenname darf nicht leer sein.");
        }

        if (string.Equals(normalizedRoleName, IdentitySeeder.AdminRoleName, StringComparison.OrdinalIgnoreCase))
        {
            return new PersonOperationResult(false, "Die Systemrolle Admin darf nicht gelöscht werden.");
        }

        if (string.Equals(normalizedRoleName, IdentitySeeder.UserRoleName, StringComparison.OrdinalIgnoreCase))
        {
            return new PersonOperationResult(false, "Die Standardrolle User darf nicht gelöscht werden.");
        }

        var role = await _roleManager.FindByNameAsync(normalizedRoleName);
        if (role is null)
        {
            return new PersonOperationResult(false, "Rolle wurde nicht gefunden.");
        }

        var usersInRole = await _userManager.GetUsersInRoleAsync(normalizedRoleName);
        if (usersInRole.Count > 0)
        {
            return new PersonOperationResult(false, "Die Rolle kann nicht gelöscht werden, solange ihr Benutzer zugewiesen sind.");
        }

        var result = await _roleManager.DeleteAsync(role);
        return result.Succeeded
            ? new PersonOperationResult(true)
            : new PersonOperationResult(false, string.Join(", ", result.Errors.Select(x => x.Description)));
    }

    private async Task<int> CountUsersInRoleAsync(string roleName, CancellationToken cancellationToken)
    {
        var users = await _userManager.GetUsersInRoleAsync(roleName);
        cancellationToken.ThrowIfCancellationRequested();
        return users.Count;
    }
}
