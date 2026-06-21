namespace Revia.Shared.Persons;

/// <summary>A person as a lightweight lookup option (for pickers across modules).</summary>
public sealed class PersonLookupDto
{
    public string Id { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
}
