using Microsoft.EntityFrameworkCore;
using PrivateLibrary.Data;

namespace PrivateLibrary.Services;

public sealed record DashboardCounts(
    int Movies,
    int Games,
    int Matches,
    int OpenMatches,
    int BmiRecords,
    int Wishlists,
    int OpenWishes,
    int TvEntries,
    int Persons);

/// <summary>Cheap per-household counts for the dashboard module tiles.</summary>
public sealed class DashboardService(AppDbContext db, CurrentUserContext currentUser)
{
    private readonly AppDbContext _db = db;
    private readonly CurrentUserContext _currentUser = currentUser;

    public async Task<DashboardCounts> GetCountsAsync(CancellationToken cancellationToken = default)
    {
        var hid = await _currentUser.RequireHouseholdIdAsync();

        return new DashboardCounts(
            Movies: await _db.Movies.CountAsync(x => x.HouseholdId == hid, cancellationToken),
            Games: await _db.Games.CountAsync(x => x.HouseholdId == hid, cancellationToken),
            Matches: await _db.GameMatches.CountAsync(x => x.HouseholdId == hid, cancellationToken),
            OpenMatches: await _db.GameMatches.CountAsync(x => x.HouseholdId == hid && !x.IsCompleted, cancellationToken),
            BmiRecords: await _db.BmiRecords.CountAsync(x => x.HouseholdId == hid, cancellationToken),
            Wishlists: await _db.Wishlists.CountAsync(x => x.HouseholdId == hid, cancellationToken),
            OpenWishes: await _db.WishlistItems.CountAsync(x => x.Wishlist!.HouseholdId == hid && !x.IsFulfilled, cancellationToken),
            TvEntries: await _db.TvProgramEntries.CountAsync(x => x.TvProgram!.HouseholdId == hid, cancellationToken),
            Persons: await _db.Users.CountAsync(x => x.HouseholdId == hid, cancellationToken));
    }
}
