using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PrivateLibrary.Data;
using PrivateLibrary.Modules.Lending.Models;

namespace PrivateLibrary.Api.Auth;

/// <summary>
/// Seeds a self-contained demo household with sample data so the app can be explored
/// without credentials (see the /api/auth/demo endpoint). Idempotent: it does nothing
/// once the demo user exists. Only runs when demo mode is enabled, and is intended for
/// a throwaway/seed database, never the real one.
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
        foreach (var role in new[] { "Admin", "User" })
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
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

        // The demo account is a normal (admin) user so every module is visible; it has
        // a known password too, but the /api/auth/demo endpoint logs in without asking.
        var demo = new ApplicationUser
        {
            UserName = DemoUserName,
            Email = "demo@revia.local",
            EmailConfirmed = true,
            FirstName = "Demo",
            LastName = "Nutzer",
            HouseholdId = household.Id
        };
        await userManager.CreateAsync(demo, "Demo123!");
        await userManager.AddToRoleAsync(demo, "Admin");

        await SeedSampleDataAsync(db, household.Id, demo.Id, ct);
        return demo;
    }

    /// <summary>Removes the demo household and everything scoped to it (data rows are
    /// household-scoped; users cascade their owned rows), so a re-seed is pristine.</summary>
    private static async Task ResetAsync(AppDbContext db, CancellationToken ct)
    {
        var household = await db.Households.FirstOrDefaultAsync(h => h.Name == DemoHouseholdName, ct);
        if (household is null)
        {
            return;
        }

        var hid = household.Id;

        // Delete household-scoped data. Children (items/photos/participants/links)
        // cascade via the configured FKs.
        db.LendingEntries.RemoveRange(db.LendingEntries.Where(x => x.HouseholdId == hid));
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

        // Movies.
        db.Movies.AddRange(
            new Movie { HouseholdId = householdId, Titel = "Inception", Jahr = "2010", Kategorie = "Sci-Fi", Fsk = "12" },
            new Movie { HouseholdId = householdId, Titel = "Der Pate", Jahr = "1972", Kategorie = "Drama", Fsk = "16" },
            new Movie { HouseholdId = householdId, Titel = "Spirited Away", Jahr = "2001", Kategorie = "Anime", Fsk = "6" });

        // A game.
        var game = new Game { HouseholdId = householdId, Name = "Catan", Publisher = "Kosmos", MinPlayers = 3, MaxPlayers = 4, CreatedAt = DateTime.UtcNow };
        db.Games.Add(game);

        // A wishlist with wishes (owned by the demo user).
        var wishlist = new Wishlist { HouseholdId = householdId, OwnerUserId = demoUserId, Name = "Geburtstag", CreatedAt = DateTime.UtcNow };
        db.Wishlists.Add(wishlist);
        await db.SaveChangesAsync(ct); // persist ids for FKs

        db.WishlistItems.AddRange(
            new WishlistItem { WishlistId = wishlist.Id, Title = "Brettspiel-Erweiterung", IsFulfilled = false },
            new WishlistItem { WishlistId = wishlist.Id, Title = "Kopfhörer", IsFulfilled = true });
        await db.SaveChangesAsync(ct);

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
