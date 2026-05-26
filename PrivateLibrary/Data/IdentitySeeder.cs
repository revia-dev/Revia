using Microsoft.AspNetCore.Identity;

namespace PrivateLibrary.Data;

public static class IdentitySeeder
{
    public const string AdminRoleName = "Admin";
    public const string UserRoleName = "User";
    public const string DefaultAdminEmail = "admin@privatelibrary.local";
    public const string DefaultAdminUserName = "admin";
    public const string DefaultAdminPassword = "Admin123!";

    public static async Task SeedAsync(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager)
    {
        await EnsureRoleExistsAsync(roleManager, AdminRoleName);
        await EnsureRoleExistsAsync(roleManager, UserRoleName);

        var adminUser = await userManager.FindByNameAsync(DefaultAdminUserName)
                        ?? await userManager.FindByEmailAsync(DefaultAdminEmail);

        if (adminUser is null)
        {
            adminUser = new ApplicationUser
            {
                UserName = DefaultAdminUserName,
                Email = DefaultAdminEmail,
                EmailConfirmed = true,
                IsSystemUser = true
            };

            var createResult = await userManager.CreateAsync(adminUser, DefaultAdminPassword);
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
    }

    private static async Task EnsureRoleExistsAsync(RoleManager<IdentityRole> roleManager, string roleName)
    {
        if (await roleManager.RoleExistsAsync(roleName))
        {
            return;
        }

        var createResult = await roleManager.CreateAsync(new IdentityRole(roleName));
        if (!createResult.Succeeded)
        {
            var errors = string.Join(", ", createResult.Errors.Select(x => $"{x.Code}: {x.Description}"));
            throw new InvalidOperationException($"Role '{roleName}' could not be created: {errors}");
        }
    }
}
