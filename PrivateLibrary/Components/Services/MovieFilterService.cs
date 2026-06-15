using PrivateLibrary.Models;

namespace PrivateLibrary.Services;

public class MovieFilterService
{
    // Rough runtime buckets, shown as their own filter group ("Länge").
    public const string RuntimeUpTo90 = "bis 90 Min";
    public const string Runtime90To120 = "90–120 Min";
    public const string Runtime120To150 = "120–150 Min";
    public const string RuntimeOver150 = "über 150 Min";

    private static readonly string[] RuntimeBuckets =
        { RuntimeUpTo90, Runtime90To120, Runtime120To150, RuntimeOver150 };

    private static bool IsRuntimeBucket(string name) => RuntimeBuckets.Contains(name);

    private static bool MatchesRuntimeBucket(Film film, string bucket)
    {
        if (film.Runtime is not int r) return false;
        return bucket switch
        {
            RuntimeUpTo90 => r <= 90,
            Runtime90To120 => r > 90 && r <= 120,
            Runtime120To150 => r > 120 && r <= 150,
            RuntimeOver150 => r > 150,
            _ => false
        };
    }

    public List<FilterGroup> BuildFilterData(IEnumerable<Film> filme)
    {
        var fskSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var anbieterSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var kategorieSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var tagsSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var film in filme)
        {
            if (!string.IsNullOrWhiteSpace(film.Fsk))
                fskSet.Add(film.Fsk);

            if (film.Anbieter is not null)
                foreach (var a in film.Anbieter)
                    anbieterSet.Add(a);

            if (!string.IsNullOrWhiteSpace(film.Kategorie))
                kategorieSet.Add(film.Kategorie);

            if (film.Tags is not null)
                foreach (var tag in film.Tags.Where(t => !t.StartsWith("#")))
                    tagsSet.Add(tag);
        }

        return new List<FilterGroup>
        {
            new()
            {
                Title = "FSK",
                Tags = fskSet
                    .OrderBy(x => ParseFskOrder(x))
                    .Select(x => new FilterTag { Name = x })
                    .ToList()
            },
            new()
            {
                Title = "Anbieter",
                Tags = anbieterSet
                    .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                    .Select(x => new FilterTag { Name = x })
                    .ToList()
            },
            new()
            {
                Title = "Kategorie",
                Tags = kategorieSet
                    .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                    .Select(x => new FilterTag { Name = x })
                    .ToList()
            },
            new()
            {
                Title = "Tags",
                Tags = tagsSet
                    .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                    .Select(x => new FilterTag { Name = x })
                    .ToList()
            },
            new()
            {
                Title = "Länge",
                Tags = filme.Any(f => f.Runtime.HasValue)
                    ? RuntimeBuckets.Select(x => new FilterTag { Name = x }).ToList()
                    : new List<FilterTag>()
            }
        };
    }

    public List<Film> ApplyFilters(
        IEnumerable<Film> filme,
        string? titleSearch,
        HashSet<FilterTag> selectedTags)
    {
        var result = filme;

        if (!string.IsNullOrWhiteSpace(titleSearch))
        {
            result = result.Where(f =>
                (!string.IsNullOrWhiteSpace(f.Titel) &&
                 f.Titel.Contains(titleSearch, StringComparison.OrdinalIgnoreCase))
                ||
                (f.Tags?.Any(t => t.Contains(titleSearch, StringComparison.OrdinalIgnoreCase)) ?? false));
        }

        if (selectedTags.Count == 0)
            return result.ToList();

        var runtimeBuckets = selectedTags.Where(t => IsRuntimeBucket(t.Name)).ToList();
        var normalTags = selectedTags.Where(t => !IsRuntimeBucket(t.Name)).ToList();

        return result.Where(film =>
            // Runtime buckets are OR'd among themselves (any selected length matches)…
            (runtimeBuckets.Count == 0 || runtimeBuckets.Any(b => MatchesRuntimeBucket(film, b.Name)))
            // …and every other selected tag must match (AND).
            && normalTags.All(tag =>
                string.Equals(film.Fsk, tag.Name, StringComparison.OrdinalIgnoreCase) ||
                (film.Anbieter?.Any(a => a.Equals(tag.Name, StringComparison.OrdinalIgnoreCase)) ?? false) ||
                string.Equals(film.Kategorie, tag.Name, StringComparison.OrdinalIgnoreCase) ||
                (film.Tags?.Any(t => t.Equals(tag.Name, StringComparison.OrdinalIgnoreCase)) ?? false)
            )).ToList();
    }

    private static int ParseFskOrder(string value)
    {
        return value switch
        {
            "0" => 0,
            "6" => 6,
            "12" => 12,
            "16" => 16,
            "18" => 18,
            _ => int.MaxValue
        };
    }
}
