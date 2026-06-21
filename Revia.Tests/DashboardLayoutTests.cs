using Revia.Services;

namespace Revia.Tests;

public class DashboardLayoutTests
{
    [Fact]
    public void Resolve_Null_ReturnsDefaultOrderAllVisible()
    {
        var layout = DashboardLayoutService.Resolve(null);

        Assert.Equal(DashboardSections.DefaultOrder.Count, layout.Count);
        Assert.Equal(DashboardSections.DefaultOrder, layout.Select(s => s.Key).ToList());
        Assert.All(layout, s => Assert.True(s.Visible));
    }

    [Fact]
    public void Resolve_RespectsOrderAndHiddenFlag()
    {
        var layout = DashboardLayoutService.Resolve("recent,-chart,modules");

        Assert.Equal(DashboardSections.Recent, layout[0].Key);
        Assert.True(layout[0].Visible);

        Assert.Equal(DashboardSections.StatusChart, layout[1].Key);
        Assert.False(layout[1].Visible);

        Assert.Equal(DashboardSections.ModuleTiles, layout[2].Key);
        Assert.True(layout[2].Visible);
    }

    [Fact]
    public void Resolve_AppendsMissingSectionsInDefaultOrder()
    {
        // Only one section stored — the rest must be appended, visible, in canonical order.
        var layout = DashboardLayoutService.Resolve("recent");

        Assert.Equal(DashboardSections.DefaultOrder.Count, layout.Count);
        Assert.Equal(DashboardSections.Recent, layout[0].Key);
        // Every known section appears exactly once.
        Assert.Equal(
            DashboardSections.DefaultOrder.OrderBy(x => x),
            layout.Select(s => s.Key).OrderBy(x => x));
    }

    [Fact]
    public void Resolve_DropsUnknownKeys_AndDeduplicates()
    {
        var layout = DashboardLayoutService.Resolve("bogus,recent,recent,modules");

        Assert.DoesNotContain(layout, s => s.Key == "bogus");
        Assert.Single(layout, s => s.Key == DashboardSections.Recent);
        Assert.Equal(DashboardSections.DefaultOrder.Count, layout.Count);
    }

    [Fact]
    public void Serialize_RoundTrips()
    {
        const string raw = "recent,-chart,modules,kpis,-overdue";
        var resolved = DashboardLayoutService.Resolve(raw);
        var serialized = DashboardLayoutService.Serialize(resolved);

        // Re-resolving the serialized form yields the same section states.
        var reResolved = DashboardLayoutService.Resolve(serialized);
        Assert.Equal(
            resolved.Select(s => (s.Key, s.Visible)),
            reResolved.Select(s => (s.Key, s.Visible)));
    }

    [Fact]
    public void Serialize_PrefixesHiddenWithDash()
    {
        var sections = new List<DashboardSectionState>
        {
            new(DashboardSections.ModuleTiles, true),
            new(DashboardSections.StatusChart, false),
        };

        Assert.Equal("modules,-chart", DashboardLayoutService.Serialize(sections));
    }
}
