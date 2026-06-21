using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Revia.Data;
using Revia.Modules.Lending.Models;

namespace Revia.Services;

/// <summary>A single option a metric exposes so the user can shape the condition.</summary>
public sealed record AnalyticsOption(string Key, string Label, AnalyticsOptionType Type, string? Default = null, IReadOnlyList<(string Value, string Label)>? Choices = null);

public enum AnalyticsOptionType { Number, Choice, Bool }

/// <summary>Metadata describing a metric the user can pick when building a widget.</summary>
public sealed record AnalyticsMetric(
    string Key,
    string Label,
    string Module,
    string Description,
    IReadOnlyList<AnalyticsOption> Options);

/// <summary>One computed row of a widget's result (a label + a numeric value).</summary>
public sealed record AnalyticsRow(string Label, double Value, string? ValueSuffix = null);

/// <summary>The evaluated output of a widget.</summary>
public sealed record AnalyticsResult(IReadOnlyList<AnalyticsRow> Rows, string? Empty = null);

/// <summary>The catalog of metrics the analytics builder offers.</summary>
public static class AnalyticsMetrics
{
    public const string LendingMostBorrowed = "lending.most-borrowed";
    public const string LendingActiveCategories = "lending.active-categories";
    public const string LendingByStatus = "lending.by-status";
    public const string WishesOpen = "wishes.open";
    public const string WishesByList = "wishes.by-list";

    public static readonly IReadOnlyList<AnalyticsMetric> All = new[]
    {
        new AnalyticsMetric(LendingMostBorrowed, "Meist ausgeliehene Dinge", AppModules.Lending,
            "Häufigste Gegenstände über alle Ausleihen.",
            new[] { new AnalyticsOption("top", "Anzahl Einträge", AnalyticsOptionType.Number, "5") }),

        new AnalyticsMetric(LendingActiveCategories, "Aktivste Kategorien", AppModules.Lending,
            "Kategorien mit den meisten Ausleihen.",
            new[] { new AnalyticsOption("top", "Anzahl Einträge", AnalyticsOptionType.Number, "5") }),

        new AnalyticsMetric(LendingByStatus, "Ausleihen nach Status", AppModules.Lending,
            "Verteilung der Ausleihen über die Status.",
            Array.Empty<AnalyticsOption>()),

        new AnalyticsMetric(WishesOpen, "Offene Wünsche", AppModules.Wishlists,
            "Noch nicht erfüllte Wünsche, neueste zuerst.",
            new[] { new AnalyticsOption("top", "Anzahl Einträge", AnalyticsOptionType.Number, "5") }),

        new AnalyticsMetric(WishesByList, "Wünsche je Liste", AppModules.Wishlists,
            "Offene Wünsche pro Wunschzettel.",
            new[] { new AnalyticsOption("top", "Anzahl Einträge", AnalyticsOptionType.Number, "5") }),
    };

    public static AnalyticsMetric? Find(string key) => All.FirstOrDefault(m => m.Key == key);
}

/// <summary>
/// CRUD for user-defined analytics widgets and evaluation of their metrics
/// against the user's household data.
/// </summary>
public sealed class AnalyticsService(
    AppDbContext db,
    CurrentUserContext currentUser,
    HouseholdLinkService links)
{
    private readonly AppDbContext _db = db;
    private readonly CurrentUserContext _currentUser = currentUser;
    private readonly HouseholdLinkService _links = links;

    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    // ---- CRUD ----

    public async Task<List<AnalyticsWidget>> GetWidgetsAsync(CancellationToken ct = default)
    {
        var userId = await _currentUser.GetUserIdAsync();
        if (string.IsNullOrEmpty(userId))
        {
            return new();
        }

        return await _db.AnalyticsWidgets
            .Where(w => w.UserId == userId)
            .OrderBy(w => w.SortOrder).ThenBy(w => w.Id)
            .ToListAsync(ct);
    }

    public async Task<AnalyticsWidget> SaveAsync(AnalyticsWidget widget, Dictionary<string, string> options, CancellationToken ct = default)
    {
        var userId = await _currentUser.GetUserIdAsync()
            ?? throw new InvalidOperationException("Kein angemeldeter Benutzer.");
        var householdId = await _currentUser.RequireHouseholdIdAsync();

        widget.OptionsJson = JsonSerializer.Serialize(options, JsonOpts);

        if (widget.Id == 0)
        {
            widget.UserId = userId;
            widget.HouseholdId = householdId;
            widget.CreatedAt = DateTime.UtcNow;
            widget.UpdatedAt = DateTime.UtcNow;
            var maxOrder = await _db.AnalyticsWidgets.Where(w => w.UserId == userId)
                .Select(w => (int?)w.SortOrder).MaxAsync(ct) ?? -1;
            widget.SortOrder = maxOrder + 1;
            _db.AnalyticsWidgets.Add(widget);
        }
        else
        {
            var existing = await _db.AnalyticsWidgets
                .FirstOrDefaultAsync(w => w.Id == widget.Id && w.UserId == userId, ct)
                ?? throw new InvalidOperationException("Analyse nicht gefunden.");
            existing.Name = widget.Name;
            existing.Metric = widget.Metric;
            existing.OptionsJson = widget.OptionsJson;
            existing.UpdatedAt = DateTime.UtcNow;
            widget = existing;
        }

        await _db.SaveChangesAsync(ct);
        return widget;
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var userId = await _currentUser.GetUserIdAsync();
        var widget = await _db.AnalyticsWidgets.FirstOrDefaultAsync(w => w.Id == id && w.UserId == userId, ct);
        if (widget is not null)
        {
            _db.AnalyticsWidgets.Remove(widget);
            await _db.SaveChangesAsync(ct);
        }
    }

    public async Task MoveAsync(int id, int delta, CancellationToken ct = default)
    {
        var widgets = await GetWidgetsAsync(ct);
        var index = widgets.FindIndex(w => w.Id == id);
        var target = index + delta;
        if (index < 0 || target < 0 || target >= widgets.Count)
        {
            return;
        }

        (widgets[index].SortOrder, widgets[target].SortOrder) = (widgets[target].SortOrder, widgets[index].SortOrder);
        await _db.SaveChangesAsync(ct);
    }

    public static Dictionary<string, string> ParseOptions(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new();
        }
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json, JsonOpts) ?? new();
        }
        catch
        {
            return new();
        }
    }

    // ---- Evaluation ----

    public async Task<AnalyticsResult> EvaluateAsync(AnalyticsWidget widget, CancellationToken ct = default)
    {
        var options = ParseOptions(widget.OptionsJson);
        int Top(int fallback = 5) =>
            options.TryGetValue("top", out var v) && int.TryParse(v, out var n) && n > 0 ? Math.Min(n, 50) : fallback;

        var householdIds = await _links.GetVisibleHouseholdIdsAsync(await _currentUser.RequireHouseholdIdAsync());

        switch (widget.Metric)
        {
            case AnalyticsMetrics.LendingMostBorrowed:
            {
                var rows = await _db.LendingItems
                    .Where(i => householdIds.Contains(i.LendingEntry!.HouseholdId))
                    .GroupBy(i => i.Name)
                    .Select(g => new { Name = g.Key, Count = g.Count() })
                    .OrderByDescending(x => x.Count)
                    .Take(Top())
                    .ToListAsync(ct);
                return Build(rows.Select(r => new AnalyticsRow(r.Name, r.Count, "×")), "Noch keine Ausleihen erfasst.");
            }

            case AnalyticsMetrics.LendingActiveCategories:
            {
                var rows = await _db.LendingEntryCategories
                    .Where(c => householdIds.Contains(c.LendingEntry!.HouseholdId) && c.LendingCategory != null)
                    .GroupBy(c => c.LendingCategory!.Name)
                    .Select(g => new { Name = g.Key, Count = g.Count() })
                    .OrderByDescending(x => x.Count)
                    .Take(Top())
                    .ToListAsync(ct);
                return Build(rows.Select(r => new AnalyticsRow(r.Name, r.Count, "×")), "Noch keine Kategorien verwendet.");
            }

            case AnalyticsMetrics.LendingByStatus:
            {
                var entries = await _db.LendingEntries
                    .Where(e => householdIds.Contains(e.HouseholdId))
                    .Select(e => e.Status)
                    .ToListAsync(ct);
                var rows = entries.GroupBy(s => s)
                    .Select(g => new AnalyticsRow(StatusLabel(g.Key), g.Count()))
                    .OrderByDescending(r => r.Value)
                    .ToList();
                return Build(rows, "Noch keine Ausleihen erfasst.");
            }

            case AnalyticsMetrics.WishesOpen:
            {
                var rows = await _db.WishlistItems
                    .Where(i => !i.IsFulfilled && householdIds.Contains(i.Wishlist!.HouseholdId))
                    .OrderByDescending(i => i.CreatedAt)
                    .Take(Top())
                    .Select(i => new { i.Title, i.Price })
                    .ToListAsync(ct);
                return Build(
                    rows.Select(r => new AnalyticsRow(r.Title, r.Price.HasValue ? (double)r.Price.Value : 0, r.Price.HasValue ? "€" : null)),
                    "Keine offenen Wünsche.");
            }

            case AnalyticsMetrics.WishesByList:
            {
                var rows = await _db.WishlistItems
                    .Where(i => !i.IsFulfilled && householdIds.Contains(i.Wishlist!.HouseholdId))
                    .GroupBy(i => i.Wishlist!.Name)
                    .Select(g => new { Name = g.Key, Count = g.Count() })
                    .OrderByDescending(x => x.Count)
                    .Take(Top())
                    .ToListAsync(ct);
                return Build(rows.Select(r => new AnalyticsRow(r.Name, r.Count)), "Keine offenen Wünsche.");
            }

            default:
                return new AnalyticsResult(Array.Empty<AnalyticsRow>(), "Unbekannte Analyse.");
        }
    }

    private static AnalyticsResult Build(IEnumerable<AnalyticsRow> rows, string emptyMessage)
    {
        var list = rows.ToList();
        return list.Count == 0
            ? new AnalyticsResult(list, emptyMessage)
            : new AnalyticsResult(list);
    }

    private static string StatusLabel(LendingEntryStatus status) => status switch
    {
        LendingEntryStatus.Active => "Aktiv",
        LendingEntryStatus.Overdue => "Überfällig",
        LendingEntryStatus.Returned => "Zurückgegeben",
        LendingEntryStatus.Lost => "Verloren",
        LendingEntryStatus.GivenAway => "Verschenkt",
        _ => status.ToString()
    };
}
