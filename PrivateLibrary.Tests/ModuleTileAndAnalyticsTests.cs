using PrivateLibrary.Services;

namespace PrivateLibrary.Tests;

public class ModuleTileLayoutTests
{
    [Fact]
    public void Resolve_Null_ReturnsAllModulesVisibleInOrder()
    {
        var tiles = ModuleTileLayoutService.Resolve(null);

        Assert.Equal(AppModules.All.Count, tiles.Count);
        Assert.Equal(AppModules.All.Select(m => m.Key), tiles.Select(t => t.Key));
        Assert.All(tiles, t => Assert.True(t.Visible));
    }

    [Fact]
    public void Resolve_RespectsOrderAndHidden()
    {
        var tiles = ModuleTileLayoutService.Resolve("games,-movies,bmi");

        Assert.Equal(AppModules.Games, tiles[0].Key);
        Assert.True(tiles[0].Visible);
        Assert.Equal(AppModules.Movies, tiles[1].Key);
        Assert.False(tiles[1].Visible);
        Assert.Equal(AppModules.Bmi, tiles[2].Key);
    }

    [Fact]
    public void Resolve_AppendsMissingModules_DropsUnknown()
    {
        var tiles = ModuleTileLayoutService.Resolve("games,bogus,games");

        Assert.Equal(AppModules.All.Count, tiles.Count);
        Assert.DoesNotContain(tiles, t => t.Key == "bogus");
        Assert.Single(tiles, t => t.Key == AppModules.Games);
    }

    [Fact]
    public void Serialize_RoundTrips()
    {
        const string raw = "games,-movies,bmi,wishlists,-tv";
        var resolved = ModuleTileLayoutService.Resolve(raw);
        var serialized = ModuleTileLayoutService.Serialize(resolved);
        var reResolved = ModuleTileLayoutService.Resolve(serialized);

        Assert.Equal(
            resolved.Select(t => (t.Key, t.Visible)),
            reResolved.Select(t => (t.Key, t.Visible)));
    }
}

public class AppModulesTests
{
    [Fact]
    public void Find_KnownKey_Resolves()
    {
        Assert.NotNull(AppModules.Find(AppModules.Movies));
        Assert.Equal("Filme", AppModules.Label(AppModules.Movies));
    }

    [Fact]
    public void Find_Unknown_ReturnsNull_LabelFallsBack()
    {
        Assert.Null(AppModules.Find("nope"));
        Assert.Equal("nope", AppModules.Label("nope"));
    }

    [Fact]
    public void ReadWritePermissionKeys_AreDistinct()
    {
        var m = AppModules.Find(AppModules.Lending)!;
        Assert.Equal("read:lending", m.ReadPermission);
        Assert.Equal("write:lending", m.WritePermission);
    }
}

public class AnalyticsServiceUnitTests
{
    [Fact]
    public void ParseOptions_Null_ReturnsEmpty()
    {
        Assert.Empty(AnalyticsService.ParseOptions(null));
        Assert.Empty(AnalyticsService.ParseOptions(""));
    }

    [Fact]
    public void ParseOptions_RoundTripsTopOption()
    {
        var json = System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, string> { ["top"] = "7" });
        var parsed = AnalyticsService.ParseOptions(json);
        Assert.Equal("7", parsed["top"]);
    }

    [Fact]
    public void ParseOptions_Invalid_ReturnsEmpty()
    {
        Assert.Empty(AnalyticsService.ParseOptions("{not json"));
    }

    [Fact]
    public void Metrics_AllHaveUniqueKeysAndKnownModules()
    {
        var keys = AnalyticsMetrics.All.Select(m => m.Key).ToList();
        Assert.Equal(keys.Count, keys.Distinct().Count());
        Assert.All(AnalyticsMetrics.All, m => Assert.NotNull(AppModules.Find(m.Module)));
    }
}
