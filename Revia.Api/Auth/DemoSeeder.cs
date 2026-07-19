using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Revia.Data;
using Revia.Modules.Lending.Models;

namespace Revia.Api.Auth;

/// <summary>
/// Seeds a self-contained demo household with sample data so the app can be explored
/// without credentials (see the /api/auth/demo endpoint). The demo is isolated in its
/// own household and can be rebuilt without changing regular users or their data.
/// </summary>
public static class DemoSeeder
{
    public const string DemoUserName = "demo";
    public const string DemoHouseholdName = "Demo-Haushalt";

    /// <summary>Ensures the demo user + sample data exist. When <paramref name="reset"/>
    /// is true, any previous demo household is wiped first, so every restart starts
    /// from a pristine demo (anything a visitor changed is discarded).</summary>
    public static async Task<ApplicationUser> SeedAsync(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        AppDbContext db,
        bool reset = true,
        CancellationToken ct = default)
    {
        // Roles (reuse the same names as the real identity seeder).
        foreach (var role in new[] { IdentitySeeder.AdminRoleName, IdentitySeeder.UserRoleName })
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                var roleResult = await roleManager.CreateAsync(new IdentityRole(role));
                EnsureSucceeded(roleResult, $"Demo role '{role}' could not be created");
            }
        }

        if (reset)
        {
            await ResetAsync(db, ct);
        }
        else
        {
            var existing = await userManager.FindByNameAsync(DemoUserName);
            if (existing is not null)
            {
                return existing; // already seeded, keep as-is
            }
        }

        var household = await db.Households.FirstOrDefaultAsync(h => h.Name == DemoHouseholdName, ct);
        if (household is null)
        {
            household = new Household { Name = DemoHouseholdName, InviteCode = Household.NewInviteCode(), CreatedAt = DateTime.UtcNow };
            db.Households.Add(household);
            await db.SaveChangesAsync(ct);
        }

        // The demo account is an administrator so every module can be explored. The
        // endpoint signs it in without exposing or relying on a shared password.
        var demo = new ApplicationUser
        {
            UserName = DemoUserName,
            Email = "demo@revia.local",
            EmailConfirmed = true,
            FirstName = "Demo",
            LastName = "Account",
            HouseholdId = household.Id,
            IsSystemUser = true
        };
        EnsureSucceeded(
            await userManager.CreateAsync(demo, $"Demo-{Guid.NewGuid():N}-aA1!"),
            "Demo account could not be created");
        EnsureSucceeded(
            await userManager.AddToRoleAsync(demo, IdentitySeeder.AdminRoleName),
            "Demo administrator role could not be assigned");

        await SeedSampleDataAsync(db, household.Id, demo.Id, ct);
        return demo;
    }

    /// <summary>Removes the demo household and everything scoped to it (data rows are
    /// household-scoped; users cascade their owned rows), so a re-seed is pristine.</summary>
    public static async Task ResetAsync(AppDbContext db, CancellationToken ct = default)
    {
        var household = await db.Households.FirstOrDefaultAsync(h => h.Name == DemoHouseholdName, ct);
        if (household is null)
        {
            return;
        }

        var hid = household.Id;

        // Delete every household root explicitly. Their child records cascade through
        // the configured foreign keys. Regular households are never selected here.
        db.HouseholdLinks.RemoveRange(db.HouseholdLinks.Where(x =>
            x.HouseholdAId == hid || x.HouseholdBId == hid));
        db.LendingEntries.RemoveRange(db.LendingEntries.Where(x => x.HouseholdId == hid));
        db.LendingCategories.RemoveRange(db.LendingCategories.Where(x => x.HouseholdId == hid));
        db.TvPrograms.RemoveRange(db.TvPrograms.Where(x => x.HouseholdId == hid));
        db.Movies.RemoveRange(db.Movies.Where(x => x.HouseholdId == hid));
        db.GameMatches.RemoveRange(db.GameMatches.Where(x => x.HouseholdId == hid));
        db.Games.RemoveRange(db.Games.Where(x => x.HouseholdId == hid));
        db.Wishlists.RemoveRange(db.Wishlists.Where(x => x.HouseholdId == hid));
        db.BmiRecords.RemoveRange(db.BmiRecords.Where(x => x.HouseholdId == hid));
        await db.SaveChangesAsync(ct);

        // Users in the demo household (the demo account + the loginless people).
        db.Users.RemoveRange(db.Users.Where(x => x.HouseholdId == hid));
        await db.SaveChangesAsync(ct);

        db.Households.Remove(household);
        await db.SaveChangesAsync(ct);
    }

    private static async Task SeedSampleDataAsync(AppDbContext db, int householdId, string demoUserId, CancellationToken ct)
    {
        // A couple of household members (loginless persons) for lendings/matches.
        // Identity requires a (normalised) unique user name + a security stamp; set
        // them explicitly since we add these users directly (not via UserManager).
        var alex = NewPerson("Alex", "Muster", householdId);
        var sam = NewPerson("Sam", "Beispiel", householdId);
        db.Users.Add(alex);
        db.Users.Add(sam);
        await db.SaveChangesAsync(ct);

        // Fictional titles keep screenshots deterministic and free of personal data.
        var nordlicht = new Movie
        {
            HouseholdId = householdId, Titel = "Nordlicht", Jahr = "2022",
            Kategorie = "Drama", Fsk = "6", Anbieter = "Mediathek", Tags = "ruhig, natur"
        };
        var sternenreise = new Movie
        {
            HouseholdId = householdId, Titel = "Sternenreise", Jahr = "2024",
            Kategorie = "Science-Fiction", Fsk = "12", Anbieter = "Streaming", Tags = "weltraum, abenteuer"
        };
        var stadtgeschichten = new Movie
        {
            HouseholdId = householdId, Titel = "Stadtgeschichten", Jahr = "2021",
            Kategorie = "Komödie", Fsk = "0", Anbieter = "Mediathek", Tags = "freundschaft"
        };
        var waldpfade = new Movie
        {
            HouseholdId = householdId, Titel = "Waldpfade", Jahr = "2023",
            Kategorie = "Dokumentation", Fsk = "0", Anbieter = "Bibliothek", Tags = "natur, wissen"
        };
        db.Movies.AddRange(nordlicht, sternenreise, stadtgeschichten, waldpfade);

        // A game with one completed match.
        var score = new GameScoreDefinition { Name = "Punkte", Unit = "Pkt.", HigherIsBetter = true };
        var game = new Game
        {
            HouseholdId = householdId,
            Name = "Punktesammler",
            Publisher = "Beispielverlag",
            MinPlayers = 2,
            MaxPlayers = 4,
            CreatedAt = DateTime.UtcNow,
            Types = new List<GameTypeAssignment> { new() { Type = GameType.Brettspiel } },
            ScoreDefinitions = new List<GameScoreDefinition> { score }
        };
        db.Games.Add(game);

        // A wishlist with wishes (owned by the demo user).
        var wishlist = new Wishlist { HouseholdId = householdId, OwnerUserId = demoUserId, Name = "Geburtstag", CreatedAt = DateTime.UtcNow };
        db.Wishlists.Add(wishlist);
        await db.SaveChangesAsync(ct); // persist ids for FKs

        db.WishlistItems.AddRange(
            new WishlistItem { WishlistId = wishlist.Id, Title = "Notizbuch", IsFulfilled = false },
            new WishlistItem { WishlistId = wishlist.Id, Title = "Kopfhörer", IsFulfilled = true });
        await db.SaveChangesAsync(ct);

        var privateProgram = new TvProgram
        {
            HouseholdId = householdId,
            OwnerUserId = demoUserId,
            Name = "Mein Programm"
        };
        privateProgram.Entries.Add(new TvProgramEntry
        {
            MovieId = nordlicht.Id,
            StartsAt = DateTime.Today.AddHours(20.25),
            Channel = "Mediathek",
            SharedWithHousehold = true
        });
        privateProgram.Entries.Add(new TvProgramEntry
        {
            MovieId = sternenreise.Id,
            StartsAt = DateTime.Today.AddDays(1).AddHours(20.15),
            Channel = "Streaming"
        });
        db.TvPrograms.Add(privateProgram);

        db.BmiRecords.AddRange(
            NewBmi(householdId, alex.Id, DateTime.Today.AddDays(-14), 72.4, 178),
            NewBmi(householdId, alex.Id, DateTime.Today, 71.8, 178));

        db.GameMatches.Add(new GameMatch
        {
            Game = game,
            HouseholdId = householdId,
            Title = "Demo-Partie",
            PlayedOn = DateTime.Today.AddDays(-2),
            StartedAt = DateTime.UtcNow.AddDays(-2),
            IsCompleted = true,
            ScoreDefinition = score,
            Participants = new List<GameMatchParticipant>
            {
                new() { PersonId = alex.Id, Score = 84 },
                new() { PersonId = sam.Id, Score = 71 }
            }
        });

        // A lending between two demo persons (added after the persons are persisted
        // so the lender/borrower foreign keys resolve).
        db.LendingEntries.Add(new LendingEntry
        {
            HouseholdId = householdId,
            Title = "Bohrmaschine",
            LenderId = demoUserId,
            BorrowerId = alex.Id,
            LoanDate = DateTime.Today.AddDays(-7),
            DueDate = DateTime.Today.AddDays(7),
            Items = new List<LendingItem> { new() { Name = "Bohrmaschine", SortOrder = 0 } }
        });

        await db.SaveChangesAsync(ct);
    }

    private static BmiRecord NewBmi(
        int householdId,
        string personId,
        DateTime date,
        double weight,
        double height) => new()
    {
        HouseholdId = householdId,
        PersonId = personId,
        MeasuredOn = date,
        WeightKg = weight,
        HeightCm = height,
        Bmi = BmiRecord.Calculate(weight, height),
        CreatedAt = DateTime.UtcNow
    };

    private static void EnsureSucceeded(IdentityResult result, string message)
    {
        if (result.Succeeded)
        {
            return;
        }

        var errors = string.Join(", ", result.Errors.Select(x => $"{x.Code}: {x.Description}"));
        throw new InvalidOperationException($"{message}: {errors}");
    }

    private static ApplicationUser NewPerson(string first, string last, int householdId) => new()
    {
        Id = Guid.NewGuid().ToString(),
        FirstName = first,
        LastName = last,
        HouseholdId = householdId,
        IsLoginless = true,
        EmailConfirmed = true,
        UserName = $"person-{Guid.NewGuid():N}",
        NormalizedUserName = $"PERSON-{Guid.NewGuid():N}".ToUpperInvariant(),
        SecurityStamp = Guid.NewGuid().ToString(),
        ConcurrencyStamp = Guid.NewGuid().ToString()
    };
}
