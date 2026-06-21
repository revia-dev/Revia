using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Revia.Data;
using Revia.Modules.Lending.Services;

namespace Revia.Modules.Lending;

/// <summary>
/// Streams a person's profile photo. The global RequireAuthenticatedUser fallback
/// policy applies, and a photo is only served when the target person belongs to the
/// caller's household.
/// </summary>
[ApiController]
public sealed class PersonPhotoController : ControllerBase
{
    [HttpGet("/persons/photo/{userId}")]
    public async Task<IResult> Get(
        string userId,
        [FromServices] AppDbContext db,
        [FromServices] IPersonPhotoStorage storage,
        CancellationToken cancellationToken)
    {
        var householdRaw = User.FindFirstValue(HouseholdClaims.HouseholdId);
        if (!int.TryParse(householdRaw, out var householdId))
        {
            return Results.NotFound();
        }

        var user = await db.Users
            .AsNoTracking()
            .Where(x => x.Id == userId && x.HouseholdId == householdId)
            .Select(x => new { x.ProfilePhotoFileName })
            .FirstOrDefaultAsync(cancellationToken);

        if (user?.ProfilePhotoFileName is null)
        {
            return Results.NotFound();
        }

        var path = storage.GetPath(user.ProfilePhotoFileName);
        if (!System.IO.File.Exists(path))
        {
            return Results.NotFound();
        }

        var contentType = Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            ".gif" => "image/gif",
            ".heic" => "image/heic",
            ".heif" => "image/heif",
            _ => "application/octet-stream"
        };

        return Results.File(path, contentType, enableRangeProcessing: true);
    }
}
