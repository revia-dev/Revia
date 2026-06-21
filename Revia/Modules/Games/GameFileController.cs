using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Revia.Data;

namespace Revia.Modules.Games;

/// <summary>Streams game rulebooks and match photos, scoped to the caller's household.</summary>
[ApiController]
public sealed class GameFileController : ControllerBase
{
    [HttpGet("/games/{gameId:int}/rules")]
    public async Task<IResult> Rules(
        int gameId,
        [FromServices] AppDbContext db,
        [FromServices] IGameFileStorage storage,
        CancellationToken cancellationToken)
    {
        if (!TryGetHousehold(out var householdId)) return Results.Unauthorized();

        var game = await db.Games
            .AsNoTracking()
            .Where(x => x.Id == gameId && x.HouseholdId == householdId)
            .Select(x => new { x.RulesFileName, x.RulesContentType, x.RulesOriginalName })
            .FirstOrDefaultAsync(cancellationToken);

        if (game?.RulesFileName is null) return Results.NotFound();

        var path = storage.GetPath(game.RulesFileName);
        if (!System.IO.File.Exists(path)) return Results.NotFound();

        return Results.File(path, game.RulesContentType ?? "application/octet-stream",
            fileDownloadName: game.RulesOriginalName, enableRangeProcessing: true);
    }

    [HttpGet("/games/expansions/file/{fileId:int}")]
    public async Task<IResult> ExpansionFile(
        int fileId,
        [FromServices] AppDbContext db,
        [FromServices] IGameFileStorage storage,
        CancellationToken cancellationToken)
    {
        if (!TryGetHousehold(out var householdId)) return Results.Unauthorized();

        var file = await db.GameExpansionFiles
            .AsNoTracking()
            .Where(x => x.Id == fileId && x.GameExpansion!.Game!.HouseholdId == householdId)
            .Select(x => new { x.FileName, x.ContentType, x.OriginalName })
            .FirstOrDefaultAsync(cancellationToken);

        if (file is null) return Results.NotFound();

        var path = storage.GetPath(file.FileName);
        if (!System.IO.File.Exists(path)) return Results.NotFound();

        return Results.File(path, file.ContentType, fileDownloadName: file.OriginalName, enableRangeProcessing: true);
    }

    [HttpGet("/games/matches/photo/{photoId:int}")]
    public async Task<IResult> MatchPhoto(
        int photoId,
        [FromServices] AppDbContext db,
        [FromServices] IGameFileStorage storage,
        CancellationToken cancellationToken)
    {
        if (!TryGetHousehold(out var householdId)) return Results.Unauthorized();

        var photo = await db.GameMatchPhotos
            .AsNoTracking()
            .Where(x => x.Id == photoId && x.GameMatch!.HouseholdId == householdId)
            .Select(x => new { x.FileName, x.ContentType })
            .FirstOrDefaultAsync(cancellationToken);

        if (photo is null) return Results.NotFound();

        var path = storage.GetPath(photo.FileName);
        if (!System.IO.File.Exists(path)) return Results.NotFound();

        return Results.File(path, photo.ContentType, enableRangeProcessing: true);
    }

    private bool TryGetHousehold(out int householdId)
        => int.TryParse(User.FindFirstValue(HouseholdClaims.HouseholdId), out householdId);
}
