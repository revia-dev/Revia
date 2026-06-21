using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Revia.Modules.Lending.Services;
using Revia.Services;
using Revia.Shared.Dashboard;

namespace Revia.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/dashboard")]
public sealed class DashboardController(
    DashboardService dashboard,
    ILendingService lending) : ControllerBase
{
    private readonly DashboardService _dashboard = dashboard;
    private readonly ILendingService _lending = lending;

    [HttpGet]
    public async Task<ActionResult<DashboardDto>> Get(CancellationToken ct)
    {
        var counts = await _dashboard.GetCountsAsync(ct);
        var stats = await _lending.GetDashboardStatsAsync(ct);

        return new DashboardDto
        {
            Movies = counts.Movies,
            Games = counts.Games,
            OpenMatches = counts.OpenMatches,
            BmiRecords = counts.BmiRecords,
            Wishlists = counts.Wishlists,
            OpenWishes = counts.OpenWishes,
            TvEntries = counts.TvEntries,
            Persons = counts.Persons,
            ActiveLendings = stats.TotalActive,
            OverdueLendings = stats.TotalOverdue,
            ReturnedLendings = stats.TotalReturned
        };
    }
}
