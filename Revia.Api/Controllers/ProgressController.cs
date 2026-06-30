using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Revia.Api.Auth;
using Revia.Data;
using Revia.Modules.Progress;
using Revia.Services;
using Revia.Shared.Progress;

namespace Revia.Api.Controllers;

/// <summary>
/// Per-user viewing progress (how far the signed-in user has watched each film, and
/// which series episode they stopped at). Part of the Movies module: anyone with
/// movie read access tracks their own progress, regardless of admin rights.
/// </summary>
[ApiController]
[Authorize]
[Route("api/progress")]
[ModuleAccess(AppModules.Movies)]
public sealed class ProgressController(WatchProgressService progress) : ControllerBase
{
    private readonly WatchProgressService _progress = progress;

    [HttpGet]
    public async Task<ActionResult<List<WatchProgressDto>>> GetAll(CancellationToken ct)
        => (await _progress.GetForUserAsync(ct)).Select(ToDto).ToList();

    [HttpPut]
    public async Task<ActionResult<WatchProgressDto>> Save([FromBody] SaveWatchProgressRequest request, CancellationToken ct)
    {
        var saved = await _progress.UpsertAsync(
            request.MovieId, request.Status, request.PositionSeconds, request.Season, request.Episode, ct);
        return saved is null ? NotFound() : ToDto(saved);
    }

    [HttpDelete("{movieId:guid}")]
    public async Task<IActionResult> Delete(Guid movieId, CancellationToken ct)
        => await _progress.DeleteAsync(movieId, ct) ? NoContent() : NotFound();

    private static WatchProgressDto ToDto(WatchProgress p) => new()
    {
        MovieId = p.MovieId,
        Status = p.Status,
        PositionSeconds = p.PositionSeconds,
        Season = p.Season,
        Episode = p.Episode,
        UpdatedAt = p.UpdatedAt,
    };
}
