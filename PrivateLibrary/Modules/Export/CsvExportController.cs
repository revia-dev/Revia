using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrivateLibrary.Data;
using PrivateLibrary.Modules.Games;

namespace PrivateLibrary.Modules.Export;

/// <summary>Household-scoped CSV export endpoints for every module.</summary>
[ApiController]
public sealed class CsvExportController : ControllerBase
{
    private bool TryHousehold(out int householdId)
        => int.TryParse(User.FindFirstValue(HouseholdClaims.HouseholdId), out householdId);

    private static IResult Csv(byte[] bytes, string name)
        => Results.File(bytes, "text/csv", $"{name}-{DateTime.Now:yyyy-MM-dd}.csv");

    private static string D(DateTime? d) => d?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty;
    private static string N(double? v) => v?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;

    [HttpGet("/persons/export.csv")]
    public async Task<IResult> Persons([FromServices] AppDbContext db, CancellationToken ct)
    {
        if (!TryHousehold(out var hid)) return Results.Unauthorized();
        var people = await db.Users.AsNoTracking().Where(x => x.HouseholdId == hid)
            .OrderBy(x => x.LastName).ThenBy(x => x.FirstName).ToListAsync(ct);

        var bytes = await CsvExport.BuildAsync(
            new[] { "Vorname", "Nachname", "Spitzname", "E-Mail", "Benutzername", "Telefon", "Straße", "Hausnummer", "PLZ", "Ort", "OhneLogin" },
            people.Select(p => new[] { p.FirstName, p.LastName, p.Nickname, p.Email, p.UserName, p.Phone, p.Street, p.HouseNumber, p.PostalCode, p.City, p.IsLoginless ? "ja" : "nein" }));
        return Csv(bytes, "personen");
    }

    [HttpGet("/lending/export.csv")]
    public async Task<IResult> Lendings([FromServices] AppDbContext db, CancellationToken ct)
    {
        if (!TryHousehold(out var hid)) return Results.Unauthorized();
        var entries = await db.LendingEntries.AsNoTracking().Where(x => x.HouseholdId == hid)
            .Include(x => x.Lender).Include(x => x.Borrower).Include(x => x.Items)
            .OrderByDescending(x => x.LoanDate).ToListAsync(ct);

        var bytes = await CsvExport.BuildAsync(
            new[] { "Titel", "Gegenstände", "Verleiher", "Entleiher", "Ausgeliehen", "Fällig", "Zurückgegeben", "Status", "Notizen" },
            entries.Select(e => new[]
            {
                e.Title, string.Join(", ", e.Items.Select(i => i.Name)),
                e.Lender?.DisplayName, e.Borrower?.DisplayName,
                D(e.LoanDate), D(e.DueDate), D(e.ReturnedAt), e.Status.ToString(), e.Notes
            }));
        return Csv(bytes, "ausleihen");
    }

    [HttpGet("/games/export.csv")]
    public async Task<IResult> Games([FromServices] AppDbContext db, CancellationToken ct)
    {
        if (!TryHousehold(out var hid)) return Results.Unauthorized();
        var games = await db.Games.AsNoTracking().Where(x => x.HouseholdId == hid)
            .Include(x => x.Types).OrderBy(x => x.Name).ToListAsync(ct);

        var bytes = await CsvExport.BuildAsync(
            new[] { "Name", "Arten", "Verlag", "Jahr", "SpielerMin", "SpielerMax", "Beschreibung" },
            games.Select(g => new[]
            {
                g.Name, string.Join(", ", g.Types.Select(t => GameLabels.Type(t.Type))),
                g.Publisher, g.ReleaseYear?.ToString(), g.MinPlayers?.ToString(), g.MaxPlayers?.ToString(), g.Description
            }));
        return Csv(bytes, "spiele");
    }

    [HttpGet("/games/matches/export.csv")]
    public async Task<IResult> Matches([FromServices] AppDbContext db, CancellationToken ct)
    {
        if (!TryHousehold(out var hid)) return Results.Unauthorized();
        var matches = await db.GameMatches.AsNoTracking().Where(x => x.HouseholdId == hid)
            .Include(x => x.Game).Include(x => x.Participants).ThenInclude(p => p.Person)
            .OrderByDescending(x => x.PlayedOn).ToListAsync(ct);

        var bytes = await CsvExport.BuildAsync(
            new[] { "Spiel", "Titel", "Datum", "Abgeschlossen", "Teilnehmer (Score)", "Notizen" },
            matches.Select(m => new[]
            {
                m.Game?.Name, m.Title, D(m.PlayedOn), m.IsCompleted ? "ja" : "nein",
                string.Join(", ", m.Participants.Select(p => p.Score is null ? p.Person?.DisplayName : $"{p.Person?.DisplayName}={N(p.Score)}")),
                m.Notes
            }));
        return Csv(bytes, "partien");
    }

    [HttpGet("/bmi/export.csv")]
    public async Task<IResult> Bmi([FromServices] AppDbContext db, CancellationToken ct)
    {
        if (!TryHousehold(out var hid)) return Results.Unauthorized();
        var records = await db.BmiRecords.AsNoTracking().Where(x => x.HouseholdId == hid)
            .Include(x => x.Person).OrderBy(x => x.MeasuredOn).ToListAsync(ct);

        var bytes = await CsvExport.BuildAsync(
            new[] { "Person", "Datum", "Gewicht_kg", "Größe_cm", "BMI" },
            records.Select(r => new[] { r.Person?.DisplayName, D(r.MeasuredOn), N(r.WeightKg), N(r.HeightCm), N(r.Bmi) }));
        return Csv(bytes, "bmi");
    }

    [HttpGet("/wishlists/export.csv")]
    public async Task<IResult> Wishlists([FromServices] AppDbContext db, CancellationToken ct)
    {
        if (!TryHousehold(out var hid)) return Results.Unauthorized();
        var items = await db.WishlistItems.AsNoTracking().Where(x => x.Wishlist!.HouseholdId == hid)
            .Include(x => x.Wishlist).ThenInclude(w => w!.OwnerUser)
            .OrderBy(x => x.Wishlist!.OwnerUserId).ThenBy(x => x.WishlistId).ToListAsync(ct);

        var bytes = await CsvExport.BuildAsync(
            new[] { "Besitzer", "Wunschzettel", "Wunsch", "Link", "Preis", "Erfüllt" },
            items.Select(i => new[]
            {
                i.Wishlist?.OwnerUser?.DisplayName, i.Wishlist?.Name, i.Title, i.Url,
                i.Price?.ToString(CultureInfo.InvariantCulture), i.IsFulfilled ? "ja" : "nein"
            }));
        return Csv(bytes, "wunschzettel");
    }

    [HttpGet("/tv/export.csv")]
    public async Task<IResult> Tv([FromServices] AppDbContext db, CancellationToken ct)
    {
        if (!TryHousehold(out var hid)) return Results.Unauthorized();
        var entries = await db.TvProgramEntries.AsNoTracking().Where(x => x.TvProgram!.HouseholdId == hid)
            .Include(x => x.Movie).Include(x => x.TvProgram).ThenInclude(p => p!.OwnerUser)
            .OrderBy(x => x.StartsAt).ToListAsync(ct);

        var bytes = await CsvExport.BuildAsync(
            new[] { "Programm", "Film", "Beginn", "Sender", "Notiz", "Freigegeben" },
            entries.Select(e => new[]
            {
                e.TvProgram?.OwnerUser?.DisplayName ?? "Familie", e.Movie?.Titel,
                e.StartsAt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
                e.Channel, e.Note, e.SharedWithHousehold ? "ja" : "nein"
            }));
        return Csv(bytes, "fernsehprogramm");
    }
}
