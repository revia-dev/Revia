using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PrivateLibrary.Data;
using PrivateLibrary.Services;
using PrivateLibrary.Shared.Tv;

namespace PrivateLibrary.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/tv")]
public sealed class TvController(TvGuideService guide) : ControllerBase
{
    private readonly TvGuideService _guide = guide;

    [HttpGet("private")]
    public async Task<ActionResult<List<TvEntryDto>>> GetPrivate(CancellationToken ct)
        => (await _guide.GetPrivateEntriesAsync(ct)).Select(ToDto).ToList();

    [HttpGet("family")]
    public async Task<ActionResult<List<TvEntryDto>>> GetFamily(CancellationToken ct)
        => (await _guide.GetHouseholdEntriesAsync(ct)).Select(ToDto).ToList();

    [HttpGet("movie-options")]
    public async Task<ActionResult<List<TvMovieOptionDto>>> MovieOptions(CancellationToken ct)
        => (await _guide.GetMovieOptionsAsync(ct))
            .Select(m => new TvMovieOptionDto { Id = m.Id, Title = m.Title, Year = m.Year })
            .ToList();

    [HttpPost]
    public async Task<IActionResult> Add([FromBody] AddTvEntryRequest request, CancellationToken ct)
    {
        if (request.Household)
        {
            await _guide.AddHouseholdEntryAsync(request.MovieId, request.StartsAt, request.Channel, request.Note, ct);
        }
        else
        {
            await _guide.AddPrivateEntryAsync(request.MovieId, request.StartsAt, request.Channel, request.Note, request.Shared, ct);
        }
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
        => await _guide.DeleteEntryAsync(id, ct) ? NoContent() : NotFound();

    [HttpPut("{id:int}/shared")]
    public async Task<IActionResult> SetShared(int id, [FromBody] SetTvSharedRequest request, CancellationToken ct)
        => await _guide.SetSharedAsync(id, request.Shared, ct) ? NoContent() : NotFound();

    private static TvEntryDto ToDto(TvProgramEntry e) => new()
    {
        Id = e.Id,
        MovieId = e.MovieId,
        MovieTitle = e.Movie?.Titel ?? "Unbekannt",
        PosterPath = e.Movie?.PosterPath,
        StartsAt = e.StartsAt,
        Channel = e.Channel,
        Note = e.Note,
        SharedWithHousehold = e.SharedWithHousehold,
        OwnerDisplayName = e.TvProgram?.OwnerUser?.DisplayName,
        HasPrivateOwner = e.TvProgram?.OwnerUserId is not null
    };
}
