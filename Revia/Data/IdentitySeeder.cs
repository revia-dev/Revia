using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Revia.Data;

public static class IdentitySeeder
{
    public const string AdminRoleName = "Admin";
    public const string UserRoleName = "User";
    public const string DefaultAdminEmail = "admin@revia.local";
    public const string DefaultAdminUserName = "admin";
    public const string DefaultHouseholdName = "Standard-Haushalt";

    public static async Task SeedAsync(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        AppDbContext db,
        IConfiguration configuration)
    {
        var adminPassword = configuration["Seed:AdminPassword"]
            ?? throw new InvalidOperationException(
                "Seed:AdminPassword is not configured. Set it via User Secrets (development) " +
                "or the SEED__ADMINPASSWORD environment variable (production).");

        await EnsureRoleExistsAsync(roleManager, AdminRoleName);
        await EnsureRoleExistsAsync(roleManager, UserRoleName);

        var defaultHousehold = await EnsureDefaultHouseholdAsync(db);

        var adminUser = await userManager.FindByNameAsync(DefaultAdminUserName)
                        ?? await userManager.FindByEmailAsync(DefaultAdminEmail);

        if (adminUser is null)
        {
            adminUser = new ApplicationUser
            {
                UserName = DefaultAdminUserName,
                Email = DefaultAdminEmail,
                EmailConfirmed = true,
                IsSystemUser = true,
                HouseholdId = defaultHousehold.Id
            };

            var createResult = await userManager.CreateAsync(adminUser, adminPassword);
            if (!createResult.Succeeded)
            {
                var errors = string.Join(", ", createResult.Errors.Select(x => $"{x.Code}: {x.Description}"));
                throw new InvalidOperationException($"Default admin user could not be created: {errors}");
            }
        }
        else if (!adminUser.IsSystemUser)
        {
            adminUser.IsSystemUser = true;
            var updateResult = await userManager.UpdateAsync(adminUser);
            if (!updateResult.Succeeded)
            {
                var errors = string.Join(", ", updateResult.Errors.Select(x => $"{x.Code}: {x.Description}"));
                throw new InvalidOperationException($"Default admin user could not be updated: {errors}");
            }
        }

        if (!await userManager.IsInRoleAsync(adminUser, AdminRoleName))
        {
            var addToRoleResult = await userManager.AddToRoleAsync(adminUser, AdminRoleName);
            if (!addToRoleResult.Succeeded)
            {
                var errors = string.Join(", ", addToRoleResult.Errors.Select(x => $"{x.Code}: {x.Description}"));
                throw new InvalidOperationException($"Default admin role assignment failed: {errors}");
            }
        }

        if (!await userManager.IsInRoleAsync(adminUser, UserRoleName))
        {
            var addToRoleResult = await userManager.AddToRoleAsync(adminUser, UserRoleName);
            if (!addToRoleResult.Succeeded)
            {
                var errors = string.Join(", ", addToRoleResult.Errors.Select(x => $"{x.Code}: {x.Description}"));
                throw new InvalidOperationException($"Default user role assignment failed: {errors}");
            }
        }

        await BackfillDefaultHouseholdAsync(db, defaultHousehold.Id);
    }

    private static async Task<Household> EnsureDefaultHouseholdAsync(AppDbContext db)
    {
        var existing = await db.Households.OrderBy(x => x.Id).FirstOrDefaultAsync();
        if (existing is not null)
        {
            return existing;
        }

        var household = new Household
        {
            Name = DefaultHouseholdName,
            InviteCode = Household.NewInviteCode(),
            CreatedAt = DateTime.UtcNow
        };

        db.Households.Add(household);
        await db.SaveChangesAsync();
        return household;
    }

    /// <summary>
    /// Assigns the default household to any person without one and to legacy lending
    /// rows created before multi-tenancy (HouseholdId == 0).
    /// </summary>
    private static async Task BackfillDefaultHouseholdAsync(AppDbContext db, int householdId)
    {
        await db.Users
            .Where(u => u.HouseholdId == null)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.HouseholdId, householdId));

        await db.LendingEntries
            .Where(e => e.HouseholdId == 0)
            .ExecuteUpdateAsync(s => s.SetProperty(e => e.HouseholdId, householdId));

        await db.LendingCategories
            .Where(c => c.HouseholdId == 0)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.HouseholdId, householdId));

        // Give legacy persons a stable QR link token (per-row GUID, so no bulk update).
        var usersWithoutToken = await db.Users
            .Where(u => u.PersonLinkToken == Guid.Empty)
            .ToListAsync();
        if (usersWithoutToken.Count > 0)
        {
            foreach (var user in usersWithoutToken)
            {
                user.PersonLinkToken = Guid.NewGuid();
            }
            await db.SaveChangesAsync();
        }
    }

    private static async Task EnsureRoleExistsAsync(RoleManager<IdentityRole> roleManager, string roleName)
    {
        if (await roleManager.RoleExistsAsync(roleName))
            return;

        var createResult = await roleManager.CreateAsync(new IdentityRole(roleName));
        if (!createResult.Succeeded)
        {
            var errors = string.Join(", ", createResult.Errors.Select(x => $"{x.Code}: {x.Description}"));
            throw new InvalidOperationException($"Role '{roleName}' could not be created: {errors}");
        }
    }
}
