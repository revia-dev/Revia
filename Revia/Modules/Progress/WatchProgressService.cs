using Microsoft.EntityFrameworkCore;
using Revia.Data;
using Revia.Services;

namespace Revia.Modules.Progress;

/// <summary>
/// Per-user viewing progress. Every household member keeps their own record of how
/// far they have watched each film/series; nothing here is shared across the
/// household. Reads and writes are always scoped to the signed-in user, so one
/// member can never see or change another's progress.
/// </summary>
public sealed class WatchProgressService(AppDbContext db, CurrentUserContext currentUser)
{
    private readonly AppDbContext _db = db;
    private readonly CurrentUserContext _currentUser = currentUser;

    public const string StatusUnwatched = "unwatched";
    public const string StatusInProgress = "inProgress";
    public const string StatusWatched = "watched";

    /// <summary>Maps any input to a known status, defaulting to "unwatched".</summary>
    public static string NormalizeStatus(string? value)
        => value is StatusUnwatched or StatusInProgress or StatusWatched ? value! : StatusUnwatched;

    public async Task<IReadOnlyList<WatchProgress>> GetForUserAsync(CancellationToken cancellationToken = default)
    {
        var householdId = await _currentUser.RequireHouseholdIdAsync();
        var userId = await RequireUserIdAsync();
        return await _db.WatchProgresses
            .AsNoTracking()
            .Where(x => x.HouseholdId == householdId && x.UserId == userId)
            .OrderByDescending(x => x.UpdatedAt)
            .ToListAsync(cancellationToken);
    }

    /// <summary>Creates or updates the current user's progress for one movie. Returns
    /// null when the movie does not exist in the user's household (so the caller can
    /// answer 404 rather than persisting an orphan row).</summary>
    public async Task<WatchProgress?> UpsertAsync(
        Guid movieId,
        string? status,
        int? positionSeconds,
        int? season,
        int? episode,
        CancellationToken cancellationToken = default)
    {
        var householdId = await _currentUser.RequireHouseholdIdAsync();
        var userId = await RequireUserIdAsync();

        var movieInHousehold = await _db.Movies
            .AnyAsync(m => m.Id == movieId && m.HouseholdId == householdId, cancellationToken);
        if (!movieInHousehold)
        {
            return null;
        }

        var existing = await _db.WatchProgresses.FirstOrDefaultAsync(
            x => x.HouseholdId == householdId && x.UserId == userId && x.MovieId == movieId,
            cancellationToken);

        if (existing is null)
        {
            existing = new WatchProgress
            {
                HouseholdId = householdId,
                UserId = userId,
                MovieId = movieId,
            };
            _db.WatchProgresses.Add(existing);
        }

        existing.Status = NormalizeStatus(status);
        // A film tracks a resume position; a series tracks season/episode. Keep only
        // the fields that make sense so the two never contradict each other.
        existing.PositionSeconds = season is null && episode is null ? positionSeconds : null;
        existing.Season = season;
        existing.Episode = episode;
        existing.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
        return existing;
    }

    public async Task<bool> DeleteAsync(Guid movieId, CancellationToken cancellationToken = default)
    {
        var householdId = await _currentUser.RequireHouseholdIdAsync();
        var userId = await RequireUserIdAsync();

        var existing = await _db.WatchProgresses.FirstOrDefaultAsync(
            x => x.HouseholdId == householdId && x.UserId == userId && x.MovieId == movieId,
            cancellationToken);
        if (existing is null)
        {
            return false;
        }

        _db.WatchProgresses.Remove(existing);
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task<string> RequireUserIdAsync()
        => await _currentUser.GetUserIdAsync()
            ?? throw new InvalidOperationException("Kein angemeldeter Benutzer.");
}
