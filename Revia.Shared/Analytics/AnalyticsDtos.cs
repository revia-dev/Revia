using System.ComponentModel.DataAnnotations;

namespace Revia.Shared.Analytics;

public enum AnalyticsOptionTypeDto { Number, Choice, Bool }

/// <summary>A metric option the builder exposes (e.g. top-N).</summary>
public sealed class AnalyticsOptionDto
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public AnalyticsOptionTypeDto Type { get; set; }
    public string? Default { get; set; }
    public List<AnalyticsChoiceDto> Choices { get; set; } = new();
}

public sealed class AnalyticsChoiceDto
{
    public string Value { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
}

/// <summary>A metric the user can pick when building a widget.</summary>
public sealed class AnalyticsMetricDto
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Module { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<AnalyticsOptionDto> Options { get; set; } = new();
}

/// <summary>One computed row of a widget's result.</summary>
public sealed class AnalyticsRowDto
{
    public string Label { get; set; } = string.Empty;
    public double Value { get; set; }
    public string? ValueSuffix { get; set; }
}

/// <summary>A user widget plus its evaluated result (so the dashboard renders in one call).</summary>
public sealed class AnalyticsWidgetDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Metric { get; set; } = string.Empty;
    public string MetricLabel { get; set; } = string.Empty;
    public Dictionary<string, string> Options { get; set; } = new();
    public int SortOrder { get; set; }
    public List<AnalyticsRowDto> Rows { get; set; } = new();
    public string? Empty { get; set; }
}

public sealed class SaveAnalyticsWidgetRequest
{
    /// <summary>0 for create; existing id for update.</summary>
    public int Id { get; set; }

    [Required, StringLength(80)]
    public string Name { get; set; } = string.Empty;

    [Required, StringLength(60)]
    public string Metric { get; set; } = string.Empty;

    public Dictionary<string, string> Options { get; set; } = new();
}

public sealed class MoveAnalyticsWidgetRequest
{
    public int Delta { get; set; }
}
