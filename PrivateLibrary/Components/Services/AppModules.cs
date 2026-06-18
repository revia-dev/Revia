namespace PrivateLibrary.Services;

/// <summary>
/// A functional module of the app. This is the single source of truth shared by
/// the dashboard module tiles, the per-module permission system and the analytics
/// builder, so all three stay in sync when a module is added or removed.
/// </summary>
public sealed record AppModule(string Key, string Label, string Route, string Icon)
{
    public string ReadPermission => "read:" + Key;
    public string WritePermission => "write:" + Key;
}

public static class AppModules
{
    public const string Movies = "movies";
    public const string Tv = "tv";
    public const string Lending = "lending";
    public const string Games = "games";
    public const string Bmi = "bmi";
    public const string Wishlists = "wishlists";
    public const string Persons = "persons";

    /// <summary>All modules in canonical display order.</summary>
    public static readonly IReadOnlyList<AppModule> All = new[]
    {
        new AppModule(Movies,    "Filme",     "/movies",          "film"),
        new AppModule(Tv,        "TV",        "/tv",              "tv"),
        new AppModule(Lending,   "Ausleihen", "/lending",         "box-arrow-right"),
        new AppModule(Games,     "Spiele",    "/games",           "controller"),
        new AppModule(Bmi,       "BMI",       "/bmi",             "activity"),
        new AppModule(Wishlists, "Wünsche",   "/wishlists",       "heart"),
        new AppModule(Persons,   "Personen",  "/lending/persons", "person"),
    };

    private static readonly Dictionary<string, AppModule> ByKey =
        All.ToDictionary(m => m.Key, StringComparer.Ordinal);

    public static AppModule? Find(string key)
        => ByKey.TryGetValue(key, out var m) ? m : null;

    public static string Label(string key) => Find(key)?.Label ?? key;
}
