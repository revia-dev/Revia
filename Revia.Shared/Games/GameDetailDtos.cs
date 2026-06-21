namespace Revia.Shared.Games;

/// <summary>A game's files: its rules document and its expansions (each with files).
/// Loaded online on demand for the game editor.</summary>
public sealed class GameDetailDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;

    public bool HasRules { get; set; }
    /// <summary>Authenticated path to the rules file, or null.</summary>
    public string? RulesUrl { get; set; }
    public string? RulesName { get; set; }

    public List<GameExpansionDto> Expansions { get; set; } = new();
}

public sealed class GameExpansionDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public List<GameExpansionFileDto> Files { get; set; } = new();
}

public sealed class GameExpansionFileDto
{
    public int Id { get; set; }
    public string? Name { get; set; }
    /// <summary>Authenticated path to download the file.</summary>
    public string Url { get; set; } = string.Empty;
}

/// <summary>Create a new expansion on a game.</summary>
public sealed class AddExpansionRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Notes { get; set; }
}

/// <summary>A file uploaded to a game's rules slot or an expansion (base64).</summary>
public sealed class UploadFileRequest
{
    public string Base64 { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/octet-stream";
    public string? FileName { get; set; }
}
