using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Revia.Api.Auth;
using Revia.Data;
using Revia.Services;
using Revia.Shared.Tv;

namespace Revia.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/tv")]
[ModuleAccess(AppModules.Tv)]
public sealed class TvController(TvGuideService guide) : ControllerBase
{
    private readonly TvGuideService _guide = guide;

    [HttpGet("private")]
    public async Task<ActionResult<List<TvEntryDto>>> GetPrivate(CancellationToken ct)
        => MapWithEpisodes(await _guide.GetPrivateEntriesAsync(ct));

    [HttpGet("family")]
    public async Task<ActionResult<List<TvEntryDto>>> GetFamily(CancellationToken ct)
        => MapWithEpisodes(await _guide.GetHouseholdEntriesAsync(ct));

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

    /// <summary>A movie counts as a TV series when its category contains "tv".</summary>
    private static bool IsSeries(TvProgramEntry e)
        => (e.Movie?.Kategorie ?? string.Empty).Contains("tv", StringComparison.OrdinalIgnoreCase);

    /// <summary>Maps entries to DTOs and derives the episode number for series: the
    /// base episode (1) plus how many earlier airings of the same series exist, so the
    /// episode counts up automatically as airings pass. Computed over the whole set so
    /// it's deterministic and needs no background job.</summary>
    private static List<TvEntryDto> MapWithEpisodes(IReadOnlyList<TvProgramEntry> entries)
    {
        // For each series movie, order its airings by time; the n-th airing is episode n.
        var episodeByEntryId = new Dictionary<int, int>();
        foreach (var group in entries.Where(IsSeries).GroupBy(e => e.MovieId))
        {
            var ordered = group.OrderBy(e => e.StartsAt).ThenBy(e => e.Id).ToList();
            for (var i = 0; i < ordered.Count; i++)
            {
                episodeByEntryId[ordered[i].Id] = i + 1; // 1-based
            }
        }

        return entries.Select(e =>
        {
            var dto = ToDto(e);
            dto.RuntimeMinutes = e.Movie?.Runtime;
            dto.IsSeries = IsSeries(e);
            if (dto.IsSeries && episodeByEntryId.TryGetValue(e.Id, out var ep))
            {
                dto.Episode = ep;
            }
            return dto;
        }).ToList();
    }

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
