using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PrivateLibrary.Services;
using PrivateLibrary.Shared.Access;

namespace PrivateLibrary.Api.Controllers;

/// <summary>
/// The current user's personal dashboard layout: order + visibility of the module
/// tiles and of the dashboard sections. Reuses the same per-user CSV storage and
/// resolve/serialize logic as the Blazor Server app.
/// </summary>
[ApiController]
[Authorize]
[Route("api/layout")]
public sealed class LayoutController(
    ModuleTileLayoutService tiles,
    DashboardLayoutService sections) : ControllerBase
{
    private readonly ModuleTileLayoutService _tiles = tiles;
    private readonly DashboardLayoutService _sections = sections;

    [HttpGet]
    public async Task<ActionResult<LayoutDto>> Get(CancellationToken ct)
    {
        var tileStates = await _tiles.GetLayoutAsync(ct);
        var sectionStates = await _sections.GetLayoutAsync(ct);

        return new LayoutDto
        {
            Tiles = tileStates.Select(t => new TileLayoutItemDto { Key = t.Key, Visible = t.Visible }).ToList(),
            Sections = sectionStates.Select(s => new SectionLayoutItemDto
            {
                Key = s.Key,
                Label = s.Label,
                Visible = s.Visible
            }).ToList()
        };
    }

    [HttpPut]
    public async Task<IActionResult> Save([FromBody] SaveLayoutRequest request, CancellationToken ct)
    {
        var tileStates = request.Tiles
            .Select(t => new ModuleTileState(t.Key, t.Visible))
            .ToList();
        var sectionStates = request.Sections
            .Select(s => new DashboardSectionState(s.Key, s.Visible))
            .ToList();

        await _tiles.SaveLayoutAsync(tileStates, ct);
        await _sections.SaveLayoutAsync(sectionStates, ct);
        return NoContent();
    }

    [HttpPost("reset")]
    public async Task<IActionResult> Reset(CancellationToken ct)
    {
        await _tiles.ResetLayoutAsync(ct);
        await _sections.ResetLayoutAsync(ct);
        return NoContent();
    }
}
