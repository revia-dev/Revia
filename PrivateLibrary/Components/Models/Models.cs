using System.Text.Json.Serialization;

namespace PrivateLibrary.Models;

public class Film
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string? Titel { get; set; }
    public string? Jahr { get; set; }
    public int? TmdbId { get; set; }
    public string? Fsk { get; set; }
    public List<string>? Anbieter { get; set; }
    public string? Kategorie { get; set; }
    public List<string>? Tags { get; set; }
    public string? Handlung { get; set; }
    public string? PosterPath { get; set; }
}

public class FilterTag : IEquatable<FilterTag>
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;

    public bool Equals(FilterTag? other)
        => other is not null &&
           string.Equals(Name, other.Name, StringComparison.OrdinalIgnoreCase);

    public override bool Equals(object? obj) => Equals(obj as FilterTag);

    public override int GetHashCode()
        => StringComparer.OrdinalIgnoreCase.GetHashCode(Name);
}

public class FilterGroup
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public List<FilterTag> Tags { get; set; } = new();
    public List<FilterGroup>? Subgroups { get; set; }
}
