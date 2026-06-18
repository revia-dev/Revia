using Microsoft.EntityFrameworkCore;
using PrivateLibrary.Data;

namespace PrivateLibrary.Services;

/// <summary>One module tile in a user's resolved tile layout.</summary>
public sealed record ModuleTileState(string Key, bool Visible)
{
    public AppModule? Module => AppModules.Find(Key);
    public string Label => Module?.Label ?? Key;
}

/// <summary>
/// Per-user order and visibility of the dashboard's module tiles. Stored as a
/// compact CSV on the user (module keys in order, '-' prefix = hidden), mirroring
/// <see cref="DashboardLayoutService"/>. Unknown keys are dropped and missing
/// modules appended visible, so it stays compatible as modules come and go.
/// </summary>
public sealed class ModuleTileLayoutService(AppDbContext db, CurrentUserContext currentUser)
{
    private readonly AppDbContext _db = db;
    private readonly CurrentUserContext _currentUser = currentUser;

    public async Task<List<ModuleTileState>> GetLayoutAsync(CancellationToken ct = default)
    {
        var userId = await _currentUser.GetUserIdAsync();
        string? raw = null;

        if (!string.IsNullOrEmpty(userId))
        {
            raw = await _db.Users
                .Where(u => u.Id == userId)
                .Select(u => u.ModuleTileLayout)
                .FirstOrDefaultAsync(ct);
        }

        return Resolve(raw);
    }

    public static List<ModuleTileState> Resolve(string? raw)
    {
        var result = new List<ModuleTileState>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        if (!string.IsNullOrWhiteSpace(raw))
        {
            foreach (var token in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var hidden = token.StartsWith('-');
                var key = hidden ? token[1..] : token;

                if (AppModules.Find(key) is not null && seen.Add(key))
                {
                    result.Add(new ModuleTileState(key, !hidden));
                }
            }
        }

        foreach (var module in AppModules.All)
        {
            if (seen.Add(module.Key))
            {
                result.Add(new ModuleTileState(module.Key, true));
            }
        }

        return result;
    }

    public static string Serialize(IEnumerable<ModuleTileState> tiles)
        => string.Join(',', tiles.Select(t => t.Visible ? t.Key : "-" + t.Key));

    public async Task SaveLayoutAsync(IReadOnlyList<ModuleTileState> tiles, CancellationToken ct = default)
    {
        var userId = await _currentUser.GetUserIdAsync();
        if (string.IsNullOrEmpty(userId))
        {
            return;
        }

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null)
        {
            return;
        }

        user.ModuleTileLayout = Serialize(tiles);
        await _db.SaveChangesAsync(ct);
    }

    public async Task ResetLayoutAsync(CancellationToken ct = default)
    {
        var userId = await _currentUser.GetUserIdAsync();
        if (string.IsNullOrEmpty(userId))
        {
            return;
        }

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null)
        {
            return;
        }

        user.ModuleTileLayout = null;
        await _db.SaveChangesAsync(ct);
    }
}
