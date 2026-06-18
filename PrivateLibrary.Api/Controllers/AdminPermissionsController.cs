using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PrivateLibrary.Modules.Lending.Services;
using PrivateLibrary.Services;
using PrivateLibrary.Shared.Access;

namespace PrivateLibrary.Api.Controllers;

/// <summary>
/// Admin-only management of per-user module grants. Mirrors the Blazor Server
/// PermissionsAdmin page: list login-capable users, view a user's (default-filled)
/// grants, and upsert a single user/module grant.
/// </summary>
[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/permissions")]
public sealed class AdminPermissionsController(
    IPersonService persons,
    PermissionService permissions) : ControllerBase
{
    private readonly IPersonService _persons = persons;
    private readonly PermissionService _permissions = permissions;

    /// <summary>Login-capable users (loginless persons excluded), ordered by name.</summary>
    [HttpGet("users")]
    public async Task<ActionResult<List<AdminUserDto>>> GetUsers(CancellationToken ct)
        => (await _persons.GetAllAsync(ct))
            .Where(u => !u.IsLoginless)
            .OrderBy(u => u.DisplayName)
            .Select(u => new AdminUserDto { Id = u.Id, DisplayName = u.DisplayName })
            .ToList();

    /// <summary>One user's grants, filled with defaults (full access) for modules
    /// without an explicit row.</summary>
    [HttpGet("users/{userId}")]
    public async Task<ActionResult<UserGrantsDto>> GetGrants(string userId, CancellationToken ct)
    {
        var grants = await _permissions.GetGrantsForUserAsync(userId, ct);
        return new UserGrantsDto
        {
            UserId = userId,
            Modules = AppModules.All.Select(m =>
            {
                var a = grants[m.Key];
                return new ModuleAccessDto { Module = m.Key, CanRead = a.CanRead, CanWrite = a.CanWrite };
            }).ToList()
        };
    }

    /// <summary>Upserts one user/module grant. Write implies read (normalised
    /// by the service).</summary>
    [HttpPut("users/{userId}")]
    public async Task<IActionResult> SetGrant(string userId, [FromBody] SetGrantRequest request, CancellationToken ct)
    {
        if (AppModules.Find(request.Module) is null)
        {
            return BadRequest("Unbekanntes Modul.");
        }

        await _permissions.SetAccessAsync(userId, request.Module, request.CanRead, request.CanWrite, ct);
        return NoContent();
    }
}
