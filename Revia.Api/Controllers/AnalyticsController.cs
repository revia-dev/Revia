using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Revia.Data;
using Revia.Services;
using Revia.Shared.Analytics;

namespace Revia.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/analytics")]
public sealed class AnalyticsController(AnalyticsService analytics) : ControllerBase
{
    private readonly AnalyticsService _analytics = analytics;

    /// <summary>The metric catalog for the widget builder.</summary>
    [HttpGet("metrics")]
    public ActionResult<List<AnalyticsMetricDto>> Metrics()
        => AnalyticsMetrics.All.Select(m => new AnalyticsMetricDto
        {
            Key = m.Key,
            Label = m.Label,
            Module = m.Module,
            Description = m.Description,
            Options = m.Options.Select(o => new AnalyticsOptionDto
            {
                Key = o.Key,
                Label = o.Label,
                Type = (AnalyticsOptionTypeDto)(int)o.Type,
                Default = o.Default,
                Choices = (o.Choices ?? Array.Empty<(string, string)>())
                    .Select(c => new AnalyticsChoiceDto { Value = c.Value, Label = c.Label }).ToList()
            }).ToList()
        }).ToList();

    /// <summary>The user's widgets, each evaluated against live data.</summary>
    [HttpGet]
    public async Task<ActionResult<List<AnalyticsWidgetDto>>> GetAll(CancellationToken ct)
    {
        var widgets = await _analytics.GetWidgetsAsync(ct);
        var result = new List<AnalyticsWidgetDto>(widgets.Count);
        foreach (var w in widgets)
        {
            AnalyticsResult eval;
            try { eval = await _analytics.EvaluateAsync(w, ct); }
            catch { eval = new AnalyticsResult(Array.Empty<AnalyticsRow>(), "Auswertung fehlgeschlagen."); }
            result.Add(ToDto(w, eval));
        }
        return result;
    }

    [HttpPost]
    public async Task<ActionResult<AnalyticsWidgetDto>> Create([FromBody] SaveAnalyticsWidgetRequest request, CancellationToken ct)
    {
        var saved = await _analytics.SaveAsync(
            new AnalyticsWidget { Name = request.Name, Metric = request.Metric }, request.Options, ct);
        var eval = await SafeEval(saved, ct);
        return ToDto(saved, eval);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<AnalyticsWidgetDto>> Update(int id, [FromBody] SaveAnalyticsWidgetRequest request, CancellationToken ct)
    {
        var saved = await _analytics.SaveAsync(
            new AnalyticsWidget { Id = id, Name = request.Name, Metric = request.Metric }, request.Options, ct);
        var eval = await SafeEval(saved, ct);
        return ToDto(saved, eval);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await _analytics.DeleteAsync(id, ct);
        return NoContent();
    }

    [HttpPut("{id:int}/move")]
    public async Task<IActionResult> Move(int id, [FromBody] MoveAnalyticsWidgetRequest request, CancellationToken ct)
    {
        await _analytics.MoveAsync(id, request.Delta, ct);
        return NoContent();
    }

    private async Task<AnalyticsResult> SafeEval(AnalyticsWidget w, CancellationToken ct)
    {
        try { return await _analytics.EvaluateAsync(w, ct); }
        catch { return new AnalyticsResult(Array.Empty<AnalyticsRow>(), "Auswertung fehlgeschlagen."); }
    }

    private static AnalyticsWidgetDto ToDto(AnalyticsWidget w, AnalyticsResult eval) => new()
    {
        Id = w.Id,
        Name = w.Name,
        Metric = w.Metric,
        MetricLabel = AnalyticsMetrics.Find(w.Metric)?.Label ?? w.Metric,
        Options = AnalyticsService.ParseOptions(w.OptionsJson),
        SortOrder = w.SortOrder,
        Rows = eval.Rows.Select(r => new AnalyticsRowDto { Label = r.Label, Value = r.Value, ValueSuffix = r.ValueSuffix }).ToList(),
        Empty = eval.Empty
    };
}
