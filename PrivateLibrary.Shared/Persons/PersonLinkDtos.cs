namespace PrivateLibrary.Shared.Persons;

/// <summary>A person in the current household, with the token used to share them
/// with (and link) another household.</summary>
public sealed class PersonCardDto
{
    public string Id { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public bool IsLoginless { get; set; }
    /// <summary>Share token; build the link "/persons/link/{LinkToken}".</summary>
    public string LinkToken { get; set; } = string.Empty;
}

/// <summary>A household currently linked to the caller's (shared lendings).</summary>
public sealed class LinkedHouseholdDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

/// <summary>Preview of a person referenced by a share token (possibly in another
/// household), shown before importing + linking.</summary>
public sealed class PersonLinkPreviewDto
{
    public bool Found { get; set; }
    public bool SameHousehold { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string? HouseholdName { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Location { get; set; }
}

/// <summary>Result of importing a shared person + linking the households.</summary>
public sealed class PersonLinkResultDto
{
    public bool Success { get; set; }
    public string? Error { get; set; }
}
