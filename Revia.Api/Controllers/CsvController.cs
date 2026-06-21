using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Revia.Api.Auth;
using Revia.Data;
using Revia.Modules.Export;
using Revia.Modules.Games;
using Revia.Modules.Lending.Models;
using Revia.Services;
using Revia.Shared.Export;

namespace Revia.Api.Controllers;

/// <summary>
/// CSV export + import for every module, household-scoped. Export produces the same
/// semicolon-separated format the old app used; import reads that format back so a CSV
/// can restore a backup or move data to another household/user. Imports add rows
/// (they don't replace); person references are matched by display name within the
/// caller's household and skipped (reported) when not found.
/// </summary>
[ApiController]
[Authorize]
[Route("api/csv")]
public sealed class CsvController(
    AppDbContext db,
    CurrentUserContext currentUser,
    PermissionService permissions) : ControllerBase
{
    private readonly AppDbContext _db = db;
    private readonly CurrentUserContext _currentUser = currentUser;
    private readonly PermissionService _permissions = permissions;

    /// <summary>Export needs the module's read right; import needs write. Returns the
    /// household id, or null + sets the action result to 403 when not allowed.</summary>
    private async Task<int?> AuthorizeAsync(string moduleKey, bool write)
    {
        var access = await _permissions.GetAccessAsync(moduleKey);
        if (write ? !access.CanWrite : !access.CanRead)
        {
            return null;
        }
        return await _currentUser.RequireHouseholdIdAsync();
    }

    private static string D(DateTime? d) => d?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty;
    private static string N(double? v) => v?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
    private static IActionResult CsvFile(byte[] bytes, string name)
        => new FileContentResult(bytes, "text/csv") { FileDownloadName = $"{name}-{DateTime.Now:yyyy-MM-dd}.csv" };

    // Resolve a person by display name within the household (for import references).
    private static string? PersonIdByName(IReadOnlyList<ApplicationUser> people, string? name)
        => string.IsNullOrWhiteSpace(name)
            ? null
            : people.FirstOrDefault(p => string.Equals(p.DisplayName, name.Trim(), StringComparison.OrdinalIgnoreCase))?.Id;

    private static List<Dictionary<string, string>> ParseUpload(IFormFile file)
    {
        using var stream = file.OpenReadStream();
        return CsvImport.Parse(stream);
    }

    // =========================================================================
    //  Persons
    // =========================================================================

    [HttpGet("persons/export")]
    public async Task<IActionResult> PersonsExport(CancellationToken ct)
    {
        if (await AuthorizeAsync(AppModules.Persons, write: false) is not int hid) return Forbid();
        var people = await _db.Users.AsNoTracking().Where(x => x.HouseholdId == hid)
            .OrderBy(x => x.LastName).ThenBy(x => x.FirstName).ToListAsync(ct);

        var bytes = await CsvExport.BuildAsync(
            new[] { "Vorname", "Nachname", "Spitzname", "E-Mail", "Telefon", "Straße", "Hausnummer", "PLZ", "Ort" },
            people.Where(p => !p.IsSystemUser).Select(p => new[] { p.FirstName, p.LastName, p.Nickname, p.Email, p.Phone, p.Street, p.HouseNumber, p.PostalCode, p.City }));
        return CsvFile(bytes, "personen");
    }

    [HttpPost("persons/import")]
    public async Task<ActionResult<CsvImportResult>> PersonsImport(IFormFile file, CancellationToken ct)
    {
        if (await AuthorizeAsync(AppModules.Persons, write: true) is not int hid) return Forbid();
        var result = new CsvImportResult();

        foreach (var row in ParseUpload(file))
        {
            var first = row.GetValueOrDefault("Vorname")?.Trim();
            var last = row.GetValueOrDefault("Nachname")?.Trim();
            if (string.IsNullOrWhiteSpace(first) && string.IsNullOrWhiteSpace(last))
            {
                result.Skipped++; continue;
            }

            _db.Users.Add(new ApplicationUser
            {
                Id = Guid.NewGuid().ToString(),
                UserName = $"person-{Guid.NewGuid():N}",
                NormalizedUserName = $"PERSON-{Guid.NewGuid():N}".ToUpperInvariant(),
                SecurityStamp = Guid.NewGuid().ToString(),
                ConcurrencyStamp = Guid.NewGuid().ToString(),
                IsLoginless = true,
                EmailConfirmed = true,
                HouseholdId = hid,
                FirstName = first ?? string.Empty,
                LastName = last ?? string.Empty,
                Nickname = row.GetValueOrDefault("Spitzname"),
                Email = Clean(row.GetValueOrDefault("E-Mail")),
                Phone = row.GetValueOrDefault("Telefon"),
                Street = row.GetValueOrDefault("Straße"),
                HouseNumber = row.GetValueOrDefault("Hausnummer"),
                PostalCode = row.GetValueOrDefault("PLZ"),
                City = row.GetValueOrDefault("Ort")
            });
            result.Imported++;
        }

        await _db.SaveChangesAsync(ct);
        return result;
    }

    // =========================================================================
    //  BMI
    // =========================================================================

    [HttpGet("bmi/export")]
    public async Task<IActionResult> BmiExport(CancellationToken ct)
    {
        if (await AuthorizeAsync(AppModules.Bmi, write: false) is not int hid) return Forbid();
        var records = await _db.BmiRecords.AsNoTracking().Where(x => x.HouseholdId == hid)
            .Include(x => x.Person).OrderBy(x => x.MeasuredOn).ToListAsync(ct);

        var bytes = await CsvExport.BuildAsync(
            new[] { "Person", "Datum", "Gewicht_kg", "Größe_cm", "BMI" },
            records.Select(r => new[] { r.Person?.DisplayName, D(r.MeasuredOn), N(r.WeightKg), N(r.HeightCm), N(r.Bmi) }));
        return CsvFile(bytes, "bmi");
    }

    [HttpPost("bmi/import")]
    public async Task<ActionResult<CsvImportResult>> BmiImport(IFormFile file, CancellationToken ct)
    {
        if (await AuthorizeAsync(AppModules.Bmi, write: true) is not int hid) return Forbid();
        var people = await _db.Users.AsNoTracking().Where(x => x.HouseholdId == hid).ToListAsync(ct);
        var result = new CsvImportResult();

        foreach (var row in ParseUpload(file))
        {
            var personId = PersonIdByName(people, row.GetValueOrDefault("Person"));
            if (personId is null)
            {
                result.Skipped++; result.Errors.Add($"Person „{row.GetValueOrDefault("Person")}\" nicht gefunden."); continue;
            }
            if (!TryDate(row.GetValueOrDefault("Datum"), out var measuredOn) ||
                !TryNum(row.GetValueOrDefault("Gewicht_kg"), out var weight) ||
                !TryNum(row.GetValueOrDefault("Größe_cm"), out var height))
            {
                result.Skipped++; continue;
            }

            var bmi = height > 0 ? Math.Round(weight / Math.Pow(height / 100.0, 2), 1) : 0;
            _db.BmiRecords.Add(new BmiRecord
            {
                HouseholdId = hid, PersonId = personId, MeasuredOn = measuredOn,
                WeightKg = weight, HeightCm = height, Bmi = bmi
            });
            result.Imported++;
        }

        await _db.SaveChangesAsync(ct);
        return result;
    }

    // =========================================================================
    //  Wishlists
    // =========================================================================

    [HttpGet("wishlists/export")]
    public async Task<IActionResult> WishlistsExport(CancellationToken ct)
    {
        if (await AuthorizeAsync(AppModules.Wishlists, write: false) is not int hid) return Forbid();
        var items = await _db.WishlistItems.AsNoTracking().Where(x => x.Wishlist!.HouseholdId == hid)
            .Include(x => x.Wishlist).ThenInclude(w => w!.OwnerUser)
            .OrderBy(x => x.WishlistId).ToListAsync(ct);

        var bytes = await CsvExport.BuildAsync(
            new[] { "Besitzer", "Wunschzettel", "Wunsch", "Link", "Preis", "Erfüllt" },
            items.Select(i => new[]
            {
                i.Wishlist?.OwnerUser?.DisplayName, i.Wishlist?.Name, i.Title, i.Url,
                i.Price?.ToString(CultureInfo.InvariantCulture), i.IsFulfilled ? "ja" : "nein"
            }));
        return CsvFile(bytes, "wunschzettel");
    }

    [HttpPost("wishlists/import")]
    public async Task<ActionResult<CsvImportResult>> WishlistsImport(IFormFile file, CancellationToken ct)
    {
        if (await AuthorizeAsync(AppModules.Wishlists, write: true) is not int hid) return Forbid();
        var ownerId = await _currentUser.GetUserIdAsync();
        var result = new CsvImportResult();

        // Group rows by wishlist name; reuse/create the list, then add its wishes.
        foreach (var group in ParseUpload(file).GroupBy(r => (r.GetValueOrDefault("Wunschzettel") ?? "Import").Trim()))
        {
            var listName = string.IsNullOrWhiteSpace(group.Key) ? "Import" : group.Key;
            var list = await _db.Wishlists.FirstOrDefaultAsync(w => w.HouseholdId == hid && w.Name == listName, ct);
            if (list is null)
            {
                list = new Wishlist { HouseholdId = hid, OwnerUserId = ownerId ?? string.Empty, Name = listName, CreatedAt = DateTime.UtcNow };
                _db.Wishlists.Add(list);
                await _db.SaveChangesAsync(ct);
            }

            foreach (var row in group)
            {
                var title = row.GetValueOrDefault("Wunsch")?.Trim();
                if (string.IsNullOrWhiteSpace(title)) { result.Skipped++; continue; }

                decimal? price = decimal.TryParse(row.GetValueOrDefault("Preis"), NumberStyles.Any, CultureInfo.InvariantCulture, out var pr) ? pr : null;
                _db.WishlistItems.Add(new WishlistItem
                {
                    WishlistId = list.Id, Title = title, Url = Clean(row.GetValueOrDefault("Link")),
                    Price = price, IsFulfilled = IsYes(row.GetValueOrDefault("Erfüllt")), CreatedAt = DateTime.UtcNow
                });
                result.Imported++;
            }
        }

        await _db.SaveChangesAsync(ct);
        return result;
    }

    // =========================================================================
    //  Movies
    // =========================================================================

    [HttpGet("movies/export")]
    public async Task<IActionResult> MoviesExport(CancellationToken ct)
    {
        if (await AuthorizeAsync(AppModules.Movies, write: false) is not int hid) return Forbid();
        var movies = await _db.Movies.AsNoTracking().Where(x => x.HouseholdId == hid)
            .OrderBy(x => x.Titel).ToListAsync(ct);

        var bytes = await CsvExport.BuildAsync(
            new[] { "Titel", "Jahr", "FSK", "Kategorie", "Anbieter", "Tags", "Laufzeit" },
            movies.Select(m => new[] { m.Titel, m.Jahr, m.Fsk, m.Kategorie, m.Anbieter, m.Tags, m.Runtime?.ToString() }));
        return CsvFile(bytes, "filme");
    }

    [HttpPost("movies/import")]
    [Authorize(Roles = "Admin")] // movies are curated centrally (admin-only writes)
    public async Task<ActionResult<CsvImportResult>> MoviesImport(IFormFile file, CancellationToken ct)
    {
        if (await AuthorizeAsync(AppModules.Movies, write: true) is not int hid) return Forbid();
        var result = new CsvImportResult();

        foreach (var row in ParseUpload(file))
        {
            var title = row.GetValueOrDefault("Titel")?.Trim();
            if (string.IsNullOrWhiteSpace(title)) { result.Skipped++; continue; }

            _db.Movies.Add(new Movie
            {
                Id = Guid.NewGuid(),
                HouseholdId = hid,
                Titel = title,
                Jahr = Clean(row.GetValueOrDefault("Jahr")),
                Fsk = Clean(row.GetValueOrDefault("FSK")),
                Kategorie = Clean(row.GetValueOrDefault("Kategorie")),
                Anbieter = Clean(row.GetValueOrDefault("Anbieter")),
                Tags = Clean(row.GetValueOrDefault("Tags")),
                Runtime = int.TryParse(row.GetValueOrDefault("Laufzeit"), out var rt) ? rt : null
            });
            result.Imported++;
        }

        await _db.SaveChangesAsync(ct);
        return result;
    }

    // =========================================================================
    //  Lending
    // =========================================================================

    [HttpGet("lending/export")]
    public async Task<IActionResult> LendingExport(CancellationToken ct)
    {
        if (await AuthorizeAsync(AppModules.Lending, write: false) is not int hid) return Forbid();
        var entries = await _db.LendingEntries.AsNoTracking().Where(x => x.HouseholdId == hid)
            .Include(x => x.Lender).Include(x => x.Borrower).Include(x => x.Items)
            .OrderByDescending(x => x.LoanDate).ToListAsync(ct);

        var bytes = await CsvExport.BuildAsync(
            new[] { "Titel", "Gegenstände", "Verleiher", "Entleiher", "Ausgeliehen", "Fällig", "Zurückgegeben", "Notizen" },
            entries.Select(e => new[]
            {
                e.Title, string.Join(", ", e.Items.Select(i => i.Name)),
                e.Lender?.DisplayName, e.Borrower?.DisplayName,
                D(e.LoanDate), D(e.DueDate), D(e.ReturnedAt), e.Notes
            }));
        return CsvFile(bytes, "ausleihen");
    }

    [HttpPost("lending/import")]
    public async Task<ActionResult<CsvImportResult>> LendingImport(IFormFile file, CancellationToken ct)
    {
        if (await AuthorizeAsync(AppModules.Lending, write: true) is not int hid) return Forbid();
        var people = await _db.Users.AsNoTracking().Where(x => x.HouseholdId == hid).ToListAsync(ct);
        var result = new CsvImportResult();

        foreach (var row in ParseUpload(file))
        {
            var lenderId = PersonIdByName(people, row.GetValueOrDefault("Verleiher"));
            var borrowerId = PersonIdByName(people, row.GetValueOrDefault("Entleiher"));
            if (lenderId is null || borrowerId is null)
            {
                result.Skipped++;
                result.Errors.Add($"Verleiher/Entleiher nicht gefunden (Zeile „{row.GetValueOrDefault("Titel")}\").");
                continue;
            }

            var items = (row.GetValueOrDefault("Gegenstände") ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select((n, i) => new LendingItem { Name = n, SortOrder = i }).ToList();

            TryDate(row.GetValueOrDefault("Ausgeliehen"), out var loan);
            var entry = new LendingEntry
            {
                HouseholdId = hid,
                Title = Clean(row.GetValueOrDefault("Titel")),
                LenderId = lenderId,
                BorrowerId = borrowerId,
                LoanDate = loan == default ? DateTime.Today : loan,
                DueDate = TryDate(row.GetValueOrDefault("Fällig"), out var due) ? due : null,
                ReturnedAt = TryDate(row.GetValueOrDefault("Zurückgegeben"), out var ret) ? ret : null,
                Notes = Clean(row.GetValueOrDefault("Notizen")),
                Items = items,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _db.LendingEntries.Add(entry);
            result.Imported++;
        }

        await _db.SaveChangesAsync(ct);
        return result;
    }

    // =========================================================================
    //  Games
    // =========================================================================

    [HttpGet("games/export")]
    public async Task<IActionResult> GamesExport(CancellationToken ct)
    {
        if (await AuthorizeAsync(AppModules.Games, write: false) is not int hid) return Forbid();
        var games = await _db.Games.AsNoTracking().Where(x => x.HouseholdId == hid)
            .Include(x => x.Types).OrderBy(x => x.Name).ToListAsync(ct);

        var bytes = await CsvExport.BuildAsync(
            new[] { "Name", "Arten", "Verlag", "Jahr", "SpielerMin", "SpielerMax", "Beschreibung" },
            games.Select(g => new[]
            {
                g.Name, string.Join(", ", g.Types.Select(t => GameLabels.Type(t.Type))),
                g.Publisher, g.ReleaseYear?.ToString(), g.MinPlayers?.ToString(), g.MaxPlayers?.ToString(), g.Description
            }));
        return CsvFile(bytes, "spiele");
    }

    [HttpPost("games/import")]
    public async Task<ActionResult<CsvImportResult>> GamesImport(IFormFile file, CancellationToken ct)
    {
        if (await AuthorizeAsync(AppModules.Games, write: true) is not int hid) return Forbid();
        var result = new CsvImportResult();

        foreach (var row in ParseUpload(file))
        {
            var name = row.GetValueOrDefault("Name")?.Trim();
            if (string.IsNullOrWhiteSpace(name)) { result.Skipped++; continue; }

            var game = new Game
            {
                HouseholdId = hid, Name = name,
                Publisher = Clean(row.GetValueOrDefault("Verlag")),
                ReleaseYear = int.TryParse(row.GetValueOrDefault("Jahr"), out var yr) ? yr : null,
                MinPlayers = int.TryParse(row.GetValueOrDefault("SpielerMin"), out var mn) ? mn : null,
                MaxPlayers = int.TryParse(row.GetValueOrDefault("SpielerMax"), out var mx) ? mx : null,
                Description = Clean(row.GetValueOrDefault("Beschreibung")),
                CreatedAt = DateTime.UtcNow
            };
            // Map type labels back to enum values.
            foreach (var label in (row.GetValueOrDefault("Arten") ?? string.Empty)
                         .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var type = GameLabels.AllTypes.FirstOrDefault(t => string.Equals(GameLabels.Type(t), label, StringComparison.OrdinalIgnoreCase));
                game.Types.Add(new GameTypeAssignment { Type = type });
            }
            _db.Games.Add(game);
            result.Imported++;
        }

        await _db.SaveChangesAsync(ct);
        return result;
    }

    // =========================================================================
    //  Matches
    // =========================================================================

    [HttpGet("matches/export")]
    public async Task<IActionResult> MatchesExport(CancellationToken ct)
    {
        if (await AuthorizeAsync(AppModules.Games, write: false) is not int hid) return Forbid();
        var matches = await _db.GameMatches.AsNoTracking().Where(x => x.HouseholdId == hid)
            .Include(x => x.Game).Include(x => x.Participants).ThenInclude(p => p.Person)
            .OrderByDescending(x => x.PlayedOn).ToListAsync(ct);

        var bytes = await CsvExport.BuildAsync(
            new[] { "Spiel", "Titel", "Datum", "Abgeschlossen", "Teilnehmer", "Notizen" },
            matches.Select(m => new[]
            {
                m.Game?.Name, m.Title, D(m.PlayedOn), m.IsCompleted ? "ja" : "nein",
                string.Join(", ", m.Participants.Select(p => p.Score is null ? p.Person?.DisplayName : $"{p.Person?.DisplayName}={N(p.Score)}")),
                m.Notes
            }));
        return CsvFile(bytes, "partien");
    }

    [HttpPost("matches/import")]
    public async Task<ActionResult<CsvImportResult>> MatchesImport(IFormFile file, CancellationToken ct)
    {
        if (await AuthorizeAsync(AppModules.Games, write: true) is not int hid) return Forbid();
        var people = await _db.Users.AsNoTracking().Where(x => x.HouseholdId == hid).ToListAsync(ct);
        var games = await _db.Games.Where(x => x.HouseholdId == hid).ToListAsync(ct);
        var result = new CsvImportResult();

        foreach (var row in ParseUpload(file))
        {
            var gameName = row.GetValueOrDefault("Spiel")?.Trim();
            var game = games.FirstOrDefault(g => string.Equals(g.Name, gameName, StringComparison.OrdinalIgnoreCase));
            if (game is null)
            {
                result.Skipped++; result.Errors.Add($"Spiel „{gameName}\" nicht gefunden."); continue;
            }

            TryDate(row.GetValueOrDefault("Datum"), out var playedOn);
            var match = new GameMatch
            {
                GameId = game.Id, HouseholdId = hid,
                Title = Clean(row.GetValueOrDefault("Titel")),
                PlayedOn = playedOn == default ? DateTime.Today : playedOn,
                IsCompleted = IsYes(row.GetValueOrDefault("Abgeschlossen")),
                Notes = Clean(row.GetValueOrDefault("Notizen")),
                StartedAt = DateTime.UtcNow
            };
            // Participants: "Name" or "Name=Score", comma-separated.
            foreach (var part in (row.GetValueOrDefault("Teilnehmer") ?? string.Empty)
                         .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var eq = part.IndexOf('=');
                var pname = (eq >= 0 ? part[..eq] : part).Trim();
                var personId = PersonIdByName(people, pname);
                if (personId is null) { continue; }
                double? score = eq >= 0 && TryNum(part[(eq + 1)..], out var sc) ? sc : null;
                match.Participants.Add(new GameMatchParticipant { PersonId = personId, Score = score });
            }
            _db.GameMatches.Add(match);
            result.Imported++;
        }

        await _db.SaveChangesAsync(ct);
        return result;
    }

    // =========================================================================
    //  TV
    // =========================================================================

    [HttpGet("tv/export")]
    public async Task<IActionResult> TvExport(CancellationToken ct)
    {
        if (await AuthorizeAsync(AppModules.Tv, write: false) is not int hid) return Forbid();
        var entries = await _db.TvProgramEntries.AsNoTracking().Where(x => x.TvProgram!.HouseholdId == hid)
            .Include(x => x.Movie).Include(x => x.TvProgram).ThenInclude(p => p!.OwnerUser)
            .OrderBy(x => x.StartsAt).ToListAsync(ct);

        var bytes = await CsvExport.BuildAsync(
            new[] { "Film", "Beginn", "Sender", "Notiz", "Freigegeben" },
            entries.Select(e => new[]
            {
                e.Movie?.Titel,
                e.StartsAt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
                e.Channel, e.Note, e.SharedWithHousehold ? "ja" : "nein"
            }));
        return CsvFile(bytes, "fernsehprogramm");
    }

    [HttpPost("tv/import")]
    public async Task<ActionResult<CsvImportResult>> TvImport(
        IFormFile file,
        [FromServices] TvGuideService guide,
        CancellationToken ct)
    {
        if (await AuthorizeAsync(AppModules.Tv, write: true) is not int hid) return Forbid();
        var movies = await _db.Movies.AsNoTracking().Where(x => x.HouseholdId == hid).ToListAsync(ct);
        var result = new CsvImportResult();

        foreach (var row in ParseUpload(file))
        {
            var movieTitle = row.GetValueOrDefault("Film")?.Trim();
            var movie = movies.FirstOrDefault(m => string.Equals(m.Titel, movieTitle, StringComparison.OrdinalIgnoreCase));
            if (movie is null)
            {
                result.Skipped++; result.Errors.Add($"Film „{movieTitle}\" nicht gefunden."); continue;
            }
            if (!TryDate(row.GetValueOrDefault("Beginn"), out var startsAt))
            {
                result.Skipped++; continue;
            }

            await guide.AddPrivateEntryAsync(movie.Id, startsAt,
                Clean(row.GetValueOrDefault("Sender")), Clean(row.GetValueOrDefault("Notiz")),
                IsYes(row.GetValueOrDefault("Freigegeben")), ct);
            result.Imported++;
        }

        return result;
    }

    // =========================================================================
    //  Helpers
    // =========================================================================

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
    private static bool IsYes(string? s) => string.Equals(s?.Trim(), "ja", StringComparison.OrdinalIgnoreCase);

    private static bool TryDate(string? s, out DateTime value)
        => DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out value)
           || DateTime.TryParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out value);

    private static bool TryNum(string? s, out double value)
        => double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out value);
}
