using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Revia.Data;

namespace Revia.Modules.Bmi;

/// <summary>Streams a BMI snapshot photo, scoped to the caller's household.</summary>
[ApiController]
public sealed class BmiPhotoController : ControllerBase
{
    [HttpGet("/bmi/photo/{id:int}")]
    public async Task<IResult> Get(
        int id,
        [FromServices] AppDbContext db,
        [FromServices] IBmiPhotoStorage storage,
        CancellationToken cancellationToken)
    {
        var householdRaw = User.FindFirstValue(HouseholdClaims.HouseholdId);
        if (!int.TryParse(householdRaw, out var householdId))
        {
            return Results.Unauthorized();
        }

        var record = await db.BmiRecords
            .AsNoTracking()
            .Where(x => x.Id == id && x.HouseholdId == householdId)
            .Select(x => new { x.PhotoFileName })
            .FirstOrDefaultAsync(cancellationToken);

        if (record?.PhotoFileName is null)
        {
            return Results.NotFound();
        }

        var path = storage.GetPath(record.PhotoFileName);
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
