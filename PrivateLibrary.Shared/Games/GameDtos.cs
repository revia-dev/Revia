using System.ComponentModel.DataAnnotations;

namespace PrivateLibrary.Shared.Games;

/// <summary>Game type, mirrors the server enum (Brettspiel=0 … Sonstiges=3).</summary>
public enum GameTypeDto { Brettspiel = 0, Konsolenspiel = 1, Kartenspiel = 2, Sonstiges = 3 }

public sealed class GameDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Publisher { get; set; }
    public int? ReleaseYear { get; set; }
    public int? MinPlayers { get; set; }
    public int? MaxPlayers { get; set; }
    public string? Description { get; set; }
    public List<GameTypeDto> Types { get; set; } = new();
    public List<GameScoreDefinitionDto> ScoreDefinitions { get; set; } = new();
    public bool HasRules { get; set; }
    public int ExpansionCount { get; set; }
    public int MatchCount { get; set; }
}

public sealed class GameScoreDefinitionDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Unit { get; set; }
    public bool HigherIsBetter { get; set; } = true;
}

public sealed class SaveGameRequest
{
    /// <summary>0 for create; the existing id for update.</summary>
    public int Id { get; set; }

    [Required, StringLength(200)]
    public string Name { get; set; } = string.Empty;
    public string? Publisher { get; set; }
    public int? ReleaseYear { get; set; }
    public int? MinPlayers { get; set; }
    public int? MaxPlayers { get; set; }
    public string? Description { get; set; }
    public List<GameTypeDto> Types { get; set; } = new();
    public List<GameScoreDefinitionDto> ScoreDefinitions { get; set; } = new();
}
