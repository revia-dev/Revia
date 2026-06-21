using System.ComponentModel.DataAnnotations;

namespace Revia.Data;

public enum GameType
{
    Brettspiel = 0,
    Konsolenspiel = 1,
    Kartenspiel = 2,
    Sonstiges = 3
}

/// <summary>A game in the household's collection, with optional rulebook, expansions and matches.</summary>
public sealed class Game
{
    public int Id { get; set; }
    public int HouseholdId { get; set; }

    [Required]
    [StringLength(150)]
    public string Name { get; set; } = string.Empty;

    [StringLength(150)]
    public string? Publisher { get; set; }

    public int? ReleaseYear { get; set; }
    public int? MinPlayers { get; set; }
    public int? MaxPlayers { get; set; }

    [StringLength(2000)]
    public string? Description { get; set; }

    // Optional rulebook file (PDF or image), stored on disk.
    [StringLength(260)]
    public string? RulesFileName { get; set; }
    [StringLength(100)]
    public string? RulesContentType { get; set; }
    [StringLength(260)]
    public string? RulesOriginalName { get; set; }

    public DateTime CreatedAt { get; set; }

    /// <summary>One or more game types (Brettspiel, Kartenspiel, …).</summary>
    public ICollection<GameTypeAssignment> Types { get; set; } = new List<GameTypeAssignment>();

    /// <summary>One or more score definitions (unit + higher/lower-is-better).</summary>
    public ICollection<GameScoreDefinition> ScoreDefinitions { get; set; } = new List<GameScoreDefinition>();

    public ICollection<GameExpansion> Expansions { get; set; } = new List<GameExpansion>();
    public ICollection<GameMatch> Matches { get; set; } = new List<GameMatch>();
}

/// <summary>Assigns one game type to a game (a game may have several).</summary>
public sealed class GameTypeAssignment
{
    public int Id { get; set; }
    public int GameId { get; set; }
    public Game? Game { get; set; }
    public GameType Type { get; set; }
}

/// <summary>A score definition for a game: a named unit and whether higher is better.</summary>
public sealed class GameScoreDefinition
{
    public int Id { get; set; }
    public int GameId { get; set; }
    public Game? Game { get; set; }

    [Required]
    [StringLength(60)]
    public string Name { get; set; } = string.Empty;

    [StringLength(40)]
    public string? Unit { get; set; }

    public bool HigherIsBetter { get; set; } = true;
}

/// <summary>An expansion belonging to a <see cref="Game"/>, with optional files.</summary>
public sealed class GameExpansion
{
    public int Id { get; set; }
    public int GameId { get; set; }
    public Game? Game { get; set; }

    [Required]
    [StringLength(150)]
    public string Name { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; }

    public ICollection<GameExpansionFile> Files { get; set; } = new List<GameExpansionFile>();
}

/// <summary>A file (image or document) attached to a <see cref="GameExpansion"/>.</summary>
public sealed class GameExpansionFile
{
    public int Id { get; set; }
    public int GameExpansionId { get; set; }
    public GameExpansion? GameExpansion { get; set; }

    [Required]
    [StringLength(260)]
    public string FileName { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string ContentType { get; set; } = string.Empty;

    [StringLength(260)]
    public string? OriginalName { get; set; }

    public DateTime CreatedAt { get; set; }
}

/// <summary>A single play (Partie) of a game. Editable until <see cref="IsCompleted"/>.</summary>
public sealed class GameMatch
{
    public int Id { get; set; }
    public int GameId { get; set; }
    public Game? Game { get; set; }

    public int HouseholdId { get; set; }

    [StringLength(150)]
    public string? Title { get; set; }

    public DateTime PlayedOn { get; set; }

    public bool IsCompleted { get; set; }

    /// <summary>Which score definition this match uses (for unit/ranking); optional.</summary>
    public int? ScoreDefinitionId { get; set; }
    public GameScoreDefinition? ScoreDefinition { get; set; }

    [StringLength(2000)]
    public string? Notes { get; set; }

    /// <summary>When the match was first created/begun (used for "recently begun").</summary>
    public DateTime StartedAt { get; set; }

    public ICollection<GameMatchPhoto> Photos { get; set; } = new List<GameMatchPhoto>();
    public ICollection<GameMatchParticipant> Participants { get; set; } = new List<GameMatchParticipant>();
}

/// <summary>A photo attached to a <see cref="GameMatch"/>.</summary>
public sealed class GameMatchPhoto
{
    public int Id { get; set; }
    public int GameMatchId { get; set; }
    public GameMatch? GameMatch { get; set; }

    [Required]
    [StringLength(260)]
    public string FileName { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string ContentType { get; set; } = string.Empty;

    [StringLength(260)]
    public string? OriginalName { get; set; }

    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>Links a person to a <see cref="GameMatch"/> with an optional per-player score.</summary>
public sealed class GameMatchParticipant
{
    public int Id { get; set; }
    public int GameMatchId { get; set; }
    public GameMatch? GameMatch { get; set; }

    public string PersonId { get; set; } = string.Empty;
    public ApplicationUser? Person { get; set; }

    /// <summary>This player's score for the match (unit/behaviour from the match's score definition).</summary>
    public double? Score { get; set; }
}
