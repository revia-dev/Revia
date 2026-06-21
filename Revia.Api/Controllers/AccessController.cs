using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Revia.Services;
using Revia.Shared.Access;

namespace Revia.Api.Controllers;

/// <summary>
/// Exposes the module catalog and the current user's effective module access, so the
/// WASM client can gate navigation, tiles and write actions. This mirrors what the
/// API itself enforces (see <c>ModuleAccessAttribute</c>) — the client uses it for UX,
/// the API uses it for security.
/// </summary>
[ApiController]
[Authorize]
[Route("api/access")]
public sealed class AccessController(PermissionService permissions, CurrentUserContext currentUser) : ControllerBase
{
    private readonly PermissionService _permissions = permissions;
    private readonly CurrentUserContext _currentUser = currentUser;

    /// <summary>The static module catalog (key/label/route/icon).</summary>
    [HttpGet("modules")]
    public ActionResult<List<ModuleInfoDto>> GetModules()
        => AppModules.All.Select(m => new ModuleInfoDto
        {
            Key = m.Key,
            Label = m.Label,
            Route = m.Route,
            Icon = m.Icon
        }).ToList();

    /// <summary>The current user's effective read/write access for every module.</summary>
    [HttpGet("me")]
    public async Task<ActionResult<MyAccessDto>> GetMyAccess()
    {
        var user = await _currentUser.GetUserAsync();
        var isAdmin = user.IsInRole("Admin");

        var modules = new List<ModuleAccessDto>();
        foreach (var module in AppModules.All)
        {
            var access = await _permissions.GetAccessAsync(module.Key);
            modules.Add(new ModuleAccessDto
            {
                Module = module.Key,
                CanRead = access.CanRead,
                CanWrite = access.CanWrite
            });
        }

        return new MyAccessDto { IsAdmin = isAdmin, Modules = modules };
    }
}
