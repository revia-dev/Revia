namespace Revia.Api;

/// <summary>
/// Public marker type that identifies the Revia.Api assembly to
/// <c>WebApplicationFactory&lt;T&gt;</c> in integration tests. A dedicated marker is
/// used instead of the top-level <c>Program</c> type because the referenced legacy
/// Revia project also defines a <c>Program</c>, which makes the generic form ambiguous.
/// </summary>
public sealed class ApiAssemblyMarker;
