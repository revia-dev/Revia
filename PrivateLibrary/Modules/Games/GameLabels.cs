using PrivateLibrary.Data;

namespace PrivateLibrary.Modules.Games;

public static class GameLabels
{
    public static string Type(GameType type) => type switch
    {
        GameType.Brettspiel => "Brettspiel",
        GameType.Konsolenspiel => "Konsolenspiel",
        GameType.Kartenspiel => "Kartenspiel",
        _ => "Sonstiges"
    };

    public static IReadOnlyList<GameType> AllTypes { get; } =
        Enum.GetValues<GameType>();
}
