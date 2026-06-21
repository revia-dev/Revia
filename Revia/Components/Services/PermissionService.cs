using Microsoft.EntityFrameworkCore;
using Revia.Data;

namespace Revia.Services;

/// <summary>Effective read/write access for one module.</summary>
public readonly record struct ModuleAccess(bool CanRead, bool CanWrite)
{
    public static readonly ModuleAccess Full = new(true, true);
    public static readonly ModuleAccess None = new(false, false);
}

/// <summary>
/// Resolves per-user, per-module permissions and lets admins manage them.
///
/// Rules:
///  - Admins always have full read + write on every module.
///  - For non-admins, an explicit <see cref="ModulePermission"/> row wins.
///  - With no row, the default is full access, so existing users keep working
///    until an admin tightens a specific module.
///
/// Scoped per circuit; the current user's grants are cached for the lifetime of
/// the circuit and refreshed on demand after the admin changes them.
/// </summary>
public sealed class PermissionService(
    AppDbContext db,
    CurrentUserContext currentUser)
{
    private readonly AppDbContext _db = db;
    private readonly CurrentUserContext _currentUser = currentUser;

    private Dictionary<string, ModuleAccess>? _cache;
    private bool _isAdmin;
    private bool _loaded;

    private async Task EnsureLoadedAsync()
    {
        if (_loaded)
        {
            return;
        }

        var user = await _currentUser.GetUserAsync();
        _isAdmin = user.IsInRole("Admin");

        var userId = user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        _cache = new Dictionary<string, ModuleAccess>(StringComparer.Ordinal);

        if (!_isAdmin && !string.IsNullOrEmpty(userId))
        {
            var rows = await _db.ModulePermissions
                .AsNoTracking()
                .Where(p => p.UserId == userId)
                .ToListAsync();

            foreach (var row in rows)
            {
                _cache[row.Module] = new ModuleAccess(row.CanRead, row.CanWrite);
            }
        }

        _loaded = true;
    }

    /// <summary>Forces the next access check to re-read from the database.</summary>
    public void Invalidate() => _loaded = false;

    public async Task<ModuleAccess> GetAccessAsync(string moduleKey)
    {
        await EnsureLoadedAsync();
        if (_isAdmin)
        {
            return ModuleAccess.Full;
        }

        // No explicit row => default full access (backward compatible).
        return _cache!.TryGetValue(moduleKey, out var access) ? access : ModuleAccess.Full;
    }

    public async Task<bool> CanReadAsync(string moduleKey) => (await GetAccessAsync(moduleKey)).CanRead;

    public async Task<bool> CanWriteAsync(string moduleKey) => (await GetAccessAsync(moduleKey)).CanWrite;

    // ---- Admin management ----

    /// <summary>Returns the explicit grants for a user, filled with defaults
    /// (full access) for any module without a row.</summary>
    public async Task<Dictionary<string, ModuleAccess>> GetGrantsForUserAsync(string userId, CancellationToken ct = default)
    {
        var rows = await _db.ModulePermissions
            .AsNoTracking()
            .Where(p => p.UserId == userId)
            .ToListAsync(ct);

        var map = new Dictionary<string, ModuleAccess>(StringComparer.Ordinal);
        foreach (var module in AppModules.All)
        {
            var row = rows.FirstOrDefault(r => r.Module == module.Key);
            map[module.Key] = row is null
                ? ModuleAccess.Full
                : new ModuleAccess(row.CanRead, row.CanWrite);
        }
        return map;
    }

    /// <summary>Upserts the access for one user/module.</summary>
    public async Task SetAccessAsync(string userId, string moduleKey, bool canRead, bool canWrite, CancellationToken ct = default)
    {
        // Writing without reading is meaningless; normalise.
        if (canWrite)
        {
            canRead = true;
        }

        var row = await _db.ModulePermissions
            .FirstOrDefaultAsync(p => p.UserId == userId && p.Module == moduleKey, ct);

        if (row is null)
        {
            row = new ModulePermission { UserId = userId, Module = moduleKey };
            _db.ModulePermissions.Add(row);
        }

        row.CanRead = canRead;
        row.CanWrite = canWrite;
        await _db.SaveChangesAsync(ct);
    }
}
