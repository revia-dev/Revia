using System.ComponentModel.DataAnnotations;

namespace PrivateLibrary.Data;

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

    public GameType Type { get; set; }

    [StringLength(150)]
    public string? Publisher { get; set; }

    public int? ReleaseYear { get; set; }
    public int? MinPlayers { get; set; }
    public int? MaxPlayers { get; set; }

    [StringLength(2000)]
    public string? Description { get; set; }

    /// <summary>Unit of the per-match score (e.g. "Punkte", "Sekunden"); behaviour below.</summary>
    [StringLength(40)]
    public string? ScoreUnit { get; set; }

    /// <summary>Whether a higher score is better (true) or lower is better (false).</summary>
    public bool ScoreHigherIsBetter { get; set; } = true;

    // Optional rulebook file (PDF or image), stored on disk.
    [StringLength(260)]
    public string? RulesFileName { get; set; }
    [StringLength(100)]
    public string? RulesContentType { get; set; }
    [StringLength(260)]
    public string? RulesOriginalName { get; set; }

    public DateTime CreatedAt { get; set; }

    public ICollection<GameExpansion> Expansions { get; set; } = new List<GameExpansion>();
    public ICollection<GameMatch> Matches { get; set; } = new List<GameMatch>();
}

/// <summary>An expansion belonging to a <see cref="Game"/>.</summary>
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

    /// <summary>Optional score; its unit/behaviour comes from the parent game.</summary>
    public double? Score { get; set; }

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

/// <summary>Links a person to a <see cref="GameMatch"/> (optional participants).</summary>
public sealed class GameMatchParticipant
{
    public int Id { get; set; }
    public int GameMatchId { get; set; }
    public GameMatch? GameMatch { get; set; }

    public string PersonId { get; set; } = string.Empty;
    public ApplicationUser? Person { get; set; }
}
