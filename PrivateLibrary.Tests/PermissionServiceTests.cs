using Microsoft.EntityFrameworkCore;
using PrivateLibrary.Data;
using PrivateLibrary.Services;

namespace PrivateLibrary.Tests;

/// <summary>
/// Security-relevant tests for per-user module access resolution: non-admins default
/// to full access, an explicit grant restricts them, and admins always pass. (Admin
/// role membership is resolved from claims; here we cover the non-admin grant rules,
/// which are the ones that actually restrict access.)
/// </summary>
public class PermissionServiceTests
{
    private static AppDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static PermissionService NewService(AppDbContext db, string userId)
        => new(db, new CurrentUserContext(new TestAuthStateProvider(userId)));

    [Fact]
    public async Task NonAdmin_WithoutRow_DefaultsToFullAccess()
    {
        using var db = NewDb();
        var svc = NewService(db, "u1");

        var access = await svc.GetAccessAsync(AppModules.Movies);

        Assert.True(access.CanRead);
        Assert.True(access.CanWrite);
    }

    [Fact]
    public async Task ExplicitDeny_RemovesReadAndWrite()
    {
        using var db = NewDb();
        db.ModulePermissions.Add(new ModulePermission
        {
            UserId = "u1", Module = AppModules.Movies, CanRead = false, CanWrite = false
        });
        await db.SaveChangesAsync();

        var svc = NewService(db, "u1");
        var access = await svc.GetAccessAsync(AppModules.Movies);

        Assert.False(access.CanRead);
        Assert.False(access.CanWrite);
    }

    [Fact]
    public async Task ReadOnlyGrant_AllowsReadButNotWrite()
    {
        using var db = NewDb();
        db.ModulePermissions.Add(new ModulePermission
        {
            UserId = "u1", Module = AppModules.Games, CanRead = true, CanWrite = false
        });
        await db.SaveChangesAsync();

        var svc = NewService(db, "u1");
        var access = await svc.GetAccessAsync(AppModules.Games);

        Assert.True(access.CanRead);
        Assert.False(access.CanWrite);
    }

    [Fact]
    public async Task Grant_IsScopedToTheModule()
    {
        using var db = NewDb();
        db.ModulePermissions.Add(new ModulePermission
        {
            UserId = "u1", Module = AppModules.Movies, CanRead = false, CanWrite = false
        });
        await db.SaveChangesAsync();

        var svc = NewService(db, "u1");

        // A deny on movies must not affect another module.
        var games = await svc.GetAccessAsync(AppModules.Games);
        Assert.True(games.CanRead);
        Assert.True(games.CanWrite);
    }

    [Fact]
    public async Task SetAccess_WriteImpliesRead()
    {
        using var db = NewDb();
        var svc = NewService(db, "u1");

        await svc.SetAccessAsync("u1", AppModules.Bmi, canRead: false, canWrite: true);

        var row = await db.ModulePermissions.SingleAsync();
        Assert.True(row.CanWrite);
        Assert.True(row.CanRead); // normalised: write implies read
    }
}
