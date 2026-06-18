using Microsoft.EntityFrameworkCore;
using PrivateLibrary.Data;

namespace PrivateLibrary.Services;

/// <summary>Stable identifiers for the dashboard's rearrangeable sections.</summary>
public static class DashboardSections
{
    public const string ModuleTiles = "modules";
    public const string Kpis = "kpis";
    public const string StatusChart = "chart";
    public const string Analytics = "analytics";
    public const string Overdue = "overdue";
    public const string Recent = "recent";

    /// <summary>Default order shown to a user who hasn't customised anything.</summary>
    public static readonly IReadOnlyList<string> DefaultOrder = new[]
    {
        ModuleTiles, Kpis, StatusChart, Analytics, Overdue, Recent
    };

    /// <summary>Human-readable German labels for the customise panel.</summary>
    public static readonly IReadOnlyDictionary<string, string> Labels = new Dictionary<string, string>
    {
        [ModuleTiles] = "Modul-Kacheln",
        [Kpis] = "Kennzahlen",
        [StatusChart] = "Statusverteilung",
        [Analytics] = "Analysen",
        [Overdue] = "Überfällige Ausleihen",
        [Recent] = "Zuletzt hinzugefügt",
    };
}

/// <summary>One dashboard section in a user's resolved layout.</summary>
public sealed record DashboardSectionState(string Key, bool Visible)
{
    public string Label => DashboardSections.Labels.TryGetValue(Key, out var l) ? l : Key;
}

/// <summary>
/// Loads, resolves and persists a user's personal dashboard layout (section order
/// and visibility). The layout is stored as a compact CSV on the user row, e.g.
/// "modules,kpis,-chart,overdue,recent" where a leading '-' marks a hidden section.
/// Unknown keys are dropped and missing keys are appended in default order, so the
/// stored value stays forward/backward compatible as sections come and go.
/// </summary>
public sealed class DashboardLayoutService(AppDbContext db, CurrentUserContext currentUser)
{
    private readonly AppDbContext _db = db;
    private readonly CurrentUserContext _currentUser = currentUser;

    /// <summary>Resolves the current user's layout, always returning every known
    /// section exactly once, in the user's order, with visibility applied.</summary>
    public async Task<List<DashboardSectionState>> GetLayoutAsync(CancellationToken ct = default)
    {
        var userId = await _currentUser.GetUserIdAsync();
        string? raw = null;

        if (!string.IsNullOrEmpty(userId))
        {
            raw = await _db.Users
                .Where(u => u.Id == userId)
                .Select(u => u.DashboardLayout)
                .FirstOrDefaultAsync(ct);
        }

        return Resolve(raw);
    }

    /// <summary>Parses a stored value into a complete, de-duplicated, default-padded
    /// list. Public + static so it is trivially unit-testable.</summary>
    public static List<DashboardSectionState> Resolve(string? raw)
    {
        var result = new List<DashboardSectionState>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        if (!string.IsNullOrWhiteSpace(raw))
        {
            foreach (var token in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var hidden = token.StartsWith('-');
                var key = hidden ? token[1..] : token;

                if (DashboardSections.Labels.ContainsKey(key) && seen.Add(key))
                {
                    result.Add(new DashboardSectionState(key, !hidden));
                }
            }
        }

        // Append any known section the stored value didn't mention (e.g. a newly
        // added section), visible by default, in canonical order.
        foreach (var key in DashboardSections.DefaultOrder)
        {
            if (seen.Add(key))
            {
                result.Add(new DashboardSectionState(key, true));
            }
        }

        return result;
    }

    /// <summary>Serializes a resolved layout back into the compact CSV form.</summary>
    public static string Serialize(IEnumerable<DashboardSectionState> sections)
        => string.Join(',', sections.Select(s => s.Visible ? s.Key : "-" + s.Key));

    /// <summary>Persists the given layout for the current user.</summary>
    public async Task SaveLayoutAsync(IReadOnlyList<DashboardSectionState> sections, CancellationToken ct = default)
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

        user.DashboardLayout = Serialize(sections);
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>Clears the user's customisation, reverting to the default layout.</summary>
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

        user.DashboardLayout = null;
        await _db.SaveChangesAsync(ct);
    }
}
