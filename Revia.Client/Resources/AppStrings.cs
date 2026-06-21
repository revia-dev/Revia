namespace Revia.Client.Resources;

/// <summary>
/// Marker type for the UI string resources (AppStrings.resx / AppStrings.en.resx).
/// Inject <c>IStringLocalizer&lt;AppStrings&gt;</c> and index it by key, e.g.
/// <c>Loc["Dashboard_Title"]</c>. German is the neutral/default culture; English is
/// the fallback. The active culture follows the browser.
/// </summary>
public sealed class AppStrings
{
}
