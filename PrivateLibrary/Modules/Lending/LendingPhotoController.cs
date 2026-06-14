using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrivateLibrary.Data;
using PrivateLibrary.Modules.Lending.Services;

namespace PrivateLibrary.Modules.Lending;

/// <summary>
/// Streams lending photos. The global RequireAuthenticatedUser fallback policy
/// applies, so photos are only served to signed-in users.
/// </summary>
[ApiController]
public sealed class LendingPhotoController : ControllerBase
{
    [HttpGet("/lending/photos/{id:int}")]
    public async Task<IResult> Get(
        int id,
        [FromServices] AppDbContext db,
        [FromServices] ILendingPhotoStorage storage,
        CancellationToken cancellationToken)
    {
        var photo = await db.LendingPhotos
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (photo is null)
        {
            return Results.NotFound();
        }

        var path = storage.GetPath(photo.FileName);
        if (!System.IO.File.Exists(path))
        {
            return Results.NotFound();
        }

        return Results.File(path, photo.ContentType, enableRangeProcessing: true);
    }
}
