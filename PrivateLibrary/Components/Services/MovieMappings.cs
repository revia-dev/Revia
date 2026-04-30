namespace PrivateLibrary.Services;

public static class MovieMappings
{
    public static readonly Dictionary<string, string> ManualImageMapping =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Test"] = "Test.jpg",
        };

    public static readonly Dictionary<string, string> UsToFskMapping =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["G"] = "0",
            ["PG"] = "6",
            ["PG-13"] = "12",
            ["R"] = "16",
            ["NC-17"] = "18"
        };

    public static List<string> NormalizeTags(IEnumerable<string> tags)
    {
        var mapping = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["adventure"] = "Abenteuer",
            ["action"] = "Action",
            ["family"] = "Familie",
            ["fantasy"] = "Fantasy",
            ["comedy"] = "Komödie",
            ["drama"] = "Drama",
            ["sci-fi"] = "Science Fiction",
            ["science fiction"] = "Science Fiction"
        };

        var normalized = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var tag in tags)
        {
            var parts = tag
                .ToLowerInvariant()
                .Replace(" & ", ",")
                .Replace(" and ", ",")
                .Replace("/", ",")
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            foreach (var part in parts)
            {
                if (mapping.TryGetValue(part, out var translated))
                    normalized.Add(translated);
                else
                    normalized.Add(char.ToUpper(part[0]) + part[1..]);
            }
        }

        return normalized.OrderBy(x => x).ToList();
    }
}
