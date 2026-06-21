using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Revia.Api.Auth;
using Revia.Services;
using Revia.Data;
using Revia.Modules.Games;
using Revia.Shared.Games;
using Revia.Shared.Media;

namespace Revia.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/games")]
[ModuleAccess(AppModules.Games)]
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

    // ---- matches ----

    [HttpGet("{gameId:int}/matches")]
    public async Task<ActionResult<List<GameMatchDto>>> GetMatches(int gameId, [FromQuery] string? search, CancellationToken ct)
        => (await _games.GetMatchesForGameAsync(gameId, search, ct)).Select(ToMatchDto).ToList();

    [HttpPost("{gameId:int}/matches")]
    public async Task<ActionResult<GameMatchDto>> CreateMatch(int gameId, [FromBody] SaveMatchRequest request, CancellationToken ct)
    {
        try
        {
            var created = await _games.CreateMatchAsync(gameId, ToMatch(request), ToParticipants(request), ct);
            var full = await _games.GetMatchAsync(created.Id, ct);
            return ToMatchDto(full ?? created);
        }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPut("matches/{matchId:int}")]
    public async Task<ActionResult<GameMatchDto>> UpdateMatch(int matchId, [FromBody] SaveMatchRequest request, CancellationToken ct)
    {
        request.Id = matchId;
        try
        {
            var updated = await _games.UpdateMatchAsync(ToMatch(request), ToParticipants(request), ct);
            if (updated is null) { return NotFound(); }
            var full = await _games.GetMatchAsync(matchId, ct);
            return ToMatchDto(full ?? updated);
        }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPut("matches/{matchId:int}/completed")]
    public async Task<IActionResult> SetMatchCompleted(int matchId, [FromBody] SetMatchCompletedRequest request, CancellationToken ct)
    {
        await _games.SetMatchCompletedAsync(matchId, request.Completed, ct);
        return NoContent();
    }

    [HttpDelete("matches/{matchId:int}")]
    public async Task<IActionResult> DeleteMatch(int matchId, CancellationToken ct)
        => await _games.DeleteMatchAsync(matchId, ct) ? NoContent() : NotFound();

    /// <summary>Streams a match photo, scoped to the caller's household.</summary>
    [HttpGet("matches/photo/{photoId:int}")]
    public async Task<IActionResult> MatchPhoto(
        int photoId,
        [FromServices] AppDbContext db,
        [FromServices] CurrentUserContext currentUser,
        [FromServices] IGameFileStorage storage,
        CancellationToken ct)
    {
        var householdId = await currentUser.RequireHouseholdIdAsync();
        var photo = await db.GameMatchPhotos.AsNoTracking()
            .Where(x => x.Id == photoId && x.GameMatch!.HouseholdId == householdId)
            .Select(x => new { x.FileName, x.ContentType })
            .FirstOrDefaultAsync(ct);

        if (photo is null) return NotFound();
        var path = storage.GetPath(photo.FileName);
        if (!System.IO.File.Exists(path)) return NotFound();
        return PhysicalFile(path, photo.ContentType);
    }

    /// <summary>Attaches a photo (base64) to an existing match.</summary>
    [HttpPost("matches/{matchId:int}/photos")]
    public async Task<IActionResult> AddMatchPhoto(int matchId, [FromBody] UploadPhotoRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Base64)) return BadRequest("Kein Foto.");
        byte[] bytes;
        try { bytes = Convert.FromBase64String(request.Base64); }
        catch (FormatException) { return BadRequest("Ungültiges Foto."); }

        using var stream = new MemoryStream(bytes);
        await _games.AddMatchPhotoAsync(matchId, stream, request.ContentType, request.FileName, ct);
        return NoContent();
    }

    /// <summary>Removes a match photo.</summary>
    [HttpDelete("matches/photo/{photoId:int}")]
    public async Task<IActionResult> RemoveMatchPhoto(int photoId, CancellationToken ct)
    {
        await _games.RemoveMatchPhotoAsync(photoId, ct);
        return NoContent();
    }

    // ---- files: rules + expansions ----

    /// <summary>A game's rules + expansions (each with files), for the editor.</summary>
    [HttpGet("{id:int}/detail")]
    public async Task<ActionResult<GameDetailDto>> GetDetail(int id, CancellationToken ct)
    {
        var game = await _games.GetGameAsync(id, ct);
        if (game is null) return NotFound();

        return new GameDetailDto
        {
            Id = game.Id,
            Name = game.Name,
            HasRules = game.RulesFileName is not null,
            RulesUrl = game.RulesFileName is not null ? $"api/games/{game.Id}/rules" : null,
            RulesName = game.RulesOriginalName,
            Expansions = (game.Expansions ?? new List<GameExpansion>())
                .OrderBy(e => e.Name)
                .Select(e => new GameExpansionDto
                {
                    Id = e.Id,
                    Name = e.Name,
                    Notes = e.Notes,
                    Files = (e.Files ?? new List<GameExpansionFile>())
                        .OrderBy(f => f.Id)
                        .Select(f => new GameExpansionFileDto
                        {
                            Id = f.Id,
                            Name = f.OriginalName,
                            Url = $"api/games/expansions/file/{f.Id}"
                        }).ToList()
                }).ToList()
        };
    }

    /// <summary>Streams a game's rules file, scoped to the caller's household.</summary>
    [HttpGet("{id:int}/rules")]
    public async Task<IActionResult> Rules(
        int id,
        [FromServices] AppDbContext db,
        [FromServices] CurrentUserContext currentUser,
        [FromServices] IGameFileStorage storage,
        CancellationToken ct)
    {
        var householdId = await currentUser.RequireHouseholdIdAsync();
        var game = await db.Games.AsNoTracking()
            .Where(x => x.Id == id && x.HouseholdId == householdId)
            .Select(x => new { x.RulesFileName, x.RulesContentType, x.RulesOriginalName })
            .FirstOrDefaultAsync(ct);

        if (game?.RulesFileName is null) return NotFound();
        var path = storage.GetPath(game.RulesFileName);
        if (!System.IO.File.Exists(path)) return NotFound();
        return PhysicalFile(path, game.RulesContentType ?? "application/octet-stream", game.RulesOriginalName);
    }

    /// <summary>Sets/replaces a game's rules file (base64).</summary>
    [HttpPost("{id:int}/rules")]
    public async Task<IActionResult> SetRules(int id, [FromBody] UploadFileRequest request, CancellationToken ct)
    {
        if (!TryDecode(request.Base64, out var bytes)) return BadRequest("Ungültige Datei.");
        using var stream = new MemoryStream(bytes);
        await _games.SetRulesAsync(id, stream, request.ContentType, request.FileName, ct);
        return NoContent();
    }

    [HttpPost("{id:int}/expansions")]
    public async Task<ActionResult<int>> AddExpansion(int id, [FromBody] AddExpansionRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name)) return BadRequest("Name fehlt.");
        var expansionId = await _games.AddExpansionAsync(id, request.Name.Trim(), request.Notes, ct);
        return expansionId;
    }

    [HttpDelete("expansions/{expansionId:int}")]
    public async Task<IActionResult> DeleteExpansion(int expansionId, CancellationToken ct)
    {
        await _games.DeleteExpansionAsync(expansionId, ct);
        return NoContent();
    }

    /// <summary>Streams an expansion file, scoped to the caller's household.</summary>
    [HttpGet("expansions/file/{fileId:int}")]
    public async Task<IActionResult> ExpansionFile(
        int fileId,
        [FromServices] AppDbContext db,
        [FromServices] CurrentUserContext currentUser,
        [FromServices] IGameFileStorage storage,
        CancellationToken ct)
    {
        var householdId = await currentUser.RequireHouseholdIdAsync();
        var file = await db.GameExpansionFiles.AsNoTracking()
            .Where(x => x.Id == fileId && x.GameExpansion!.Game!.HouseholdId == householdId)
            .Select(x => new { x.FileName, x.ContentType, x.OriginalName })
            .FirstOrDefaultAsync(ct);

        if (file is null) return NotFound();
        var path = storage.GetPath(file.FileName);
        if (!System.IO.File.Exists(path)) return NotFound();
        return PhysicalFile(path, file.ContentType, file.OriginalName);
    }

    [HttpPost("expansions/{expansionId:int}/files")]
    public async Task<IActionResult> AddExpansionFile(int expansionId, [FromBody] UploadFileRequest request, CancellationToken ct)
    {
        if (!TryDecode(request.Base64, out var bytes)) return BadRequest("Ungültige Datei.");
        using var stream = new MemoryStream(bytes);
        await _games.AddExpansionFileAsync(expansionId, stream, request.ContentType, request.FileName, ct);
        return NoContent();
    }

    [HttpDelete("expansions/file/{fileId:int}")]
    public async Task<IActionResult> RemoveExpansionFile(int fileId, CancellationToken ct)
    {
        await _games.RemoveExpansionFileAsync(fileId, ct);
        return NoContent();
    }

    private static bool TryDecode(string base64, out byte[] bytes)
    {
        bytes = Array.Empty<byte>();
        if (string.IsNullOrWhiteSpace(base64)) return false;
        try { bytes = Convert.FromBase64String(base64); return true; }
        catch (FormatException) { return false; }
    }

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

    private static GameMatch ToMatch(SaveMatchRequest r) => new()
    {
        Id = r.Id,
        GameId = r.GameId,
        Title = r.Title,
        PlayedOn = r.PlayedOn == default ? DateTime.Today : r.PlayedOn,
        ScoreDefinitionId = r.ScoreDefinitionId,
        Notes = r.Notes
    };

    private static IEnumerable<MatchParticipantInput> ToParticipants(SaveMatchRequest r)
        => r.Participants
            .Where(p => !string.IsNullOrWhiteSpace(p.PersonId))
            .Select(p => new MatchParticipantInput(p.PersonId, p.Score));

    private static GameMatchDto ToMatchDto(GameMatch m) => new()
    {
        Id = m.Id,
        GameId = m.GameId,
        Title = m.Title,
        PlayedOn = m.PlayedOn,
        IsCompleted = m.IsCompleted,
        ScoreDefinitionId = m.ScoreDefinitionId,
        Notes = m.Notes,
        Participants = m.Participants
            .Select(p => new MatchParticipantDto
            {
                PersonId = p.PersonId,
                PersonDisplayName = p.Person?.DisplayName ?? string.Empty,
                Score = p.Score
            }).ToList(),
        PhotoUrls = (m.Photos ?? new List<GameMatchPhoto>())
            .OrderBy(p => p.SortOrder).ThenBy(p => p.Id)
            .Select(p => $"api/games/matches/photo/{p.Id}")
            .ToList()
    };
}
