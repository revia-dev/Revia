using MovieLibraryApp.Models;

namespace MovieLibraryApp.Services;

public class MovieFilterService
{
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

        return result.Where(film =>
            selectedTags.All(tag =>
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
