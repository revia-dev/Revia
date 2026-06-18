using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using PrivateLibrary.Services;

namespace PrivateLibrary.Api.Auth;

/// <summary>
/// Enforces per-user module permissions on a controller. Safe (GET/HEAD) requests
/// require <c>CanRead</c>; mutating requests require <c>CanWrite</c>. Admins always
/// pass (PermissionService grants them full access). Failing read returns 403 so the
/// client can hide the module; failing write returns 403 as well.
///
/// Unlike the Blazor Server app, which only gated the UI, this closes the API itself
/// so a direct request from the WASM client (or anything else) is authorised too.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class ModuleAccessAttribute(string moduleKey) : Attribute, IAsyncAuthorizationFilter
{
    private readonly string _moduleKey = moduleKey;

    private static readonly HashSet<string> SafeMethods =
        new(StringComparer.OrdinalIgnoreCase) { "GET", "HEAD", "OPTIONS" };

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        // [Authorize] runs first; an unauthenticated request never reaches here with
        // a real user, but guard anyway.
        if (context.HttpContext.User?.Identity?.IsAuthenticated != true)
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        var permissions = context.HttpContext.RequestServices.GetRequiredService<PermissionService>();
        var access = await permissions.GetAccessAsync(_moduleKey);

        var isSafe = SafeMethods.Contains(context.HttpContext.Request.Method);
        var allowed = isSafe ? access.CanRead : access.CanWrite;

        if (!allowed)
        {
            context.Result = new ForbidResult();
        }
    }
}
