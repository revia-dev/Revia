using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PrivateLibrary.Data;
using PrivateLibrary.Modules.Games;
using PrivateLibrary.Shared.Games;

namespace PrivateLibrary.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/games")]
public sealed class GamesController(GameService games) : ControllerBase
{
    private readonly GameService _games = games;

    [HttpGet]
    public async Task<ActionResult<List<GameDto>>> GetAll([FromQuery] string? search, CancellationToken ct)
        => (await _games.GetGamesAsync(search, ct)).Select(ToDto).ToList();

    [HttpGet("{id:int}")]
    public async Task<ActionResult<GameDto>> GetById(int id, CancellationToken ct)
    {
        var game = await _games.GetGameAsync(id, ct);
        return game is null ? NotFound() : ToDto(game);
    }

    [HttpPost]
    public async Task<ActionResult<GameDto>> Create([FromBody] SaveGameRequest request, CancellationToken ct)
    {
        var created = await _games.CreateGameAsync(ToGame(request), ToTypes(request), ToScores(request), ct);
        var full = await _games.GetGameAsync(created.Id, ct);
        return ToDto(full ?? created);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<GameDto>> Update(int id, [FromBody] SaveGameRequest request, CancellationToken ct)
    {
        request.Id = id;
        var updated = await _games.UpdateGameAsync(ToGame(request), ToTypes(request), ToScores(request), ct);
        if (updated is null) { return NotFound(); }
        var full = await _games.GetGameAsync(id, ct);
        return ToDto(full ?? updated);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
        => await _games.DeleteGameAsync(id, ct) ? NoContent() : NotFound();

    // ---- mapping ----

    private static Game ToGame(SaveGameRequest r) => new()
    {
        Id = r.Id,
        Name = r.Name,
        Publisher = r.Publisher,
        ReleaseYear = r.ReleaseYear,
        MinPlayers = r.MinPlayers,
        MaxPlayers = r.MaxPlayers,
        Description = r.Description
    };

    private static IEnumerable<GameType> ToTypes(SaveGameRequest r)
        => r.Types.Select(t => (GameType)(int)t).Distinct();

    private static IEnumerable<GameScoreDefinition> ToScores(SaveGameRequest r)
        => r.ScoreDefinitions.Select(s => new GameScoreDefinition
        {
            Id = s.Id,
            Name = s.Name,
            Unit = s.Unit,
            HigherIsBetter = s.HigherIsBetter
        });

    private static GameDto ToDto(Game g) => new()
    {
        Id = g.Id,
        Name = g.Name,
        Publisher = g.Publisher,
        ReleaseYear = g.ReleaseYear,
        MinPlayers = g.MinPlayers,
        MaxPlayers = g.MaxPlayers,
        Description = g.Description,
        Types = g.Types.Select(t => (GameTypeDto)(int)t.Type).ToList(),
        ScoreDefinitions = g.ScoreDefinitions.Select(s => new GameScoreDefinitionDto
        {
            Id = s.Id, Name = s.Name, Unit = s.Unit, HigherIsBetter = s.HigherIsBetter
        }).ToList(),
        HasRules = g.RulesFileName is not null,
        ExpansionCount = g.Expansions?.Count ?? 0,
        MatchCount = g.Matches?.Count ?? 0
    };
}
