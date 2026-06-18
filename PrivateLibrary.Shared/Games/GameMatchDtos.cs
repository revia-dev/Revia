using System.ComponentModel.DataAnnotations;

namespace PrivateLibrary.Shared.Games;

public sealed class GameMatchDto
{
    public int Id { get; set; }
    public int GameId { get; set; }
    public string? Title { get; set; }
    public DateTime PlayedOn { get; set; }
    public bool IsCompleted { get; set; }
    public int? ScoreDefinitionId { get; set; }
    public string? Notes { get; set; }
    public List<MatchParticipantDto> Participants { get; set; } = new();
}

public sealed class MatchParticipantDto
{
    public string PersonId { get; set; } = string.Empty;
    public string PersonDisplayName { get; set; } = string.Empty;
    public double? Score { get; set; }
}

public sealed class SaveMatchRequest
{
    /// <summary>0 for create; the existing id for update.</summary>
    public int Id { get; set; }

    [Required]
    public int GameId { get; set; }

    [StringLength(150)]
    public string? Title { get; set; }

    public DateTime PlayedOn { get; set; }
    public int? ScoreDefinitionId { get; set; }
    public string? Notes { get; set; }
    public List<MatchParticipantInputDto> Participants { get; set; } = new();
}

public sealed class MatchParticipantInputDto
{
    [Required]
    public string PersonId { get; set; } = string.Empty;
    public double? Score { get; set; }
}

public sealed class SetMatchCompletedRequest
{
    public bool Completed { get; set; }
}
