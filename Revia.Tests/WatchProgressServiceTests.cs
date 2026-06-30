using Microsoft.EntityFrameworkCore;
using Revia.Data;
using Revia.Modules.Progress;
using Revia.Services;

namespace Revia.Tests;

/// <summary>
/// Per-user watch progress: upsert behaviour, film vs. series field handling, and —
/// most importantly — that progress is private to each user and scoped to the
/// household (one member can't see or change another's, and can't attach progress to
/// a movie outside their household).
/// </summary>
public class WatchProgressServiceTests
{
    private const int Household = 1;
    private const int OtherHousehold = 2;

    private static AppDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static WatchProgressService NewService(AppDbContext db, string userId, int household = Household)
        => new(db, new CurrentUserContext(new TestAuthStateProvider(userId, household)));

    private static async Task<Guid> SeedMovieAsync(AppDbContext db, int household = Household)
    {
        var movie = new Movie { Id = Guid.NewGuid(), HouseholdId = household, Titel = "Dune" };
        db.Movies.Add(movie);
        await db.SaveChangesAsync();
        return movie.Id;
    }

    [Fact]
    public async Task Upsert_CreatesThenUpdates_SameRow()
    {
        using var db = NewDb();
        var movieId = await SeedMovieAsync(db);
        var svc = NewService(db, "u1");

        var first = await svc.UpsertAsync(movieId, "inProgress", positionSeconds: 600, season: null, episode: null);
        var second = await svc.UpsertAsync(movieId, "watched", positionSeconds: null, season: null, episode: null);

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal(1, await db.WatchProgresses.CountAsync());
        Assert.Equal("watched", second!.Status);
    }

    [Fact]
    public async Task Upsert_Series_KeepsEpisode_DropsPosition()
    {
        using var db = NewDb();
        var movieId = await SeedMovieAsync(db);
        var svc = NewService(db, "u1");

        var saved = await svc.UpsertAsync(movieId, "inProgress", positionSeconds: 999, season: 2, episode: 5);

        Assert.NotNull(saved);
        Assert.Equal(2, saved!.Season);
        Assert.Equal(5, saved.Episode);
        Assert.Null(saved.PositionSeconds); // a series tracks episode, not a resume minute
    }

    [Fact]
    public async Task Upsert_InvalidStatus_NormalizesToUnwatched()
    {
        using var db = NewDb();
        var movieId = await SeedMovieAsync(db);
        var svc = NewService(db, "u1");

        var saved = await svc.UpsertAsync(movieId, "nonsense", null, null, null);

        Assert.Equal("unwatched", saved!.Status);
    }

    [Fact]
    public async Task Upsert_MovieInAnotherHousehold_ReturnsNull()
    {
        using var db = NewDb();
        var foreignMovie = await SeedMovieAsync(db, OtherHousehold);
        var svc = NewService(db, "u1", Household);

        var saved = await svc.UpsertAsync(foreignMovie, "watched", null, null, null);

        Assert.Null(saved);
        Assert.Equal(0, await db.WatchProgresses.CountAsync());
    }

    [Fact]
    public async Task Progress_IsPrivatePerUser()
    {
        using var db = NewDb();
        var movieId = await SeedMovieAsync(db);

        await NewService(db, "u1").UpsertAsync(movieId, "watched", null, null, null);
        await NewService(db, "u2").UpsertAsync(movieId, "inProgress", 120, null, null);

        var u1 = await NewService(db, "u1").GetForUserAsync();
        var u2 = await NewService(db, "u2").GetForUserAsync();

        Assert.Single(u1);
        Assert.Single(u2);
        Assert.Equal("watched", u1[0].Status);
        Assert.Equal("inProgress", u2[0].Status);
        Assert.Equal(2, await db.WatchProgresses.CountAsync());
    }

    [Fact]
    public async Task Delete_RemovesOnlyOwnRow()
    {
        using var db = NewDb();
        var movieId = await SeedMovieAsync(db);
        await NewService(db, "u1").UpsertAsync(movieId, "watched", null, null, null);
        await NewService(db, "u2").UpsertAsync(movieId, "watched", null, null, null);

        var removed = await NewService(db, "u1").DeleteAsync(movieId);

        Assert.True(removed);
        Assert.False(await db.WatchProgresses.AnyAsync(x => x.UserId == "u1"));
        Assert.True(await db.WatchProgresses.AnyAsync(x => x.UserId == "u2"));
    }
}
