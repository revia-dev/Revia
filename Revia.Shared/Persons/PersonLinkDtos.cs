namespace Revia.Shared.Persons;

/// <summary>A person in the current household, with the token used to share them
/// with (and link) another household.</summary>
public sealed class PersonCardDto
{
    public string Id { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? Nickname { get; set; }
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

/// <summary>Create/update a person. Any user may manage loginless people in their
/// household; only admins may grant a login (username/password) via the separate
/// upgrade request.</summary>
public sealed class SavePersonRequest
{
    /// <summary>Empty/null for create; the existing id for update.</summary>
    public string? Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? Nickname { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Street { get; set; }
    public string? HouseNumber { get; set; }
    public string? PostalCode { get; set; }
    public string? City { get; set; }

    /// <summary>Optional login (admins only, on create): when a username + password
    /// are given, the person is created with a login instead of loginless.</summary>
    public string? UserName { get; set; }
    public string? Password { get; set; }
    public string? Role { get; set; }
}

/// <summary>Admin-only: gives an existing loginless person a login (username +
/// password + role), so they can sign in.</summary>
public sealed class AddLoginRequest
{
    public string UserName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Role { get; set; } = "User";
}

/// <summary>Generic person operation result.</summary>
public sealed class PersonResultDto
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string? Id { get; set; }
}
