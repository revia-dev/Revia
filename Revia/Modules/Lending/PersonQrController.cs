using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Revia.Data;
using QRCoder;

namespace Revia.Modules.Lending;

/// <summary>
/// Renders a person's QR code as a PNG. The code encodes a link that, when scanned
/// and opened in another household, imports the person and links the households.
/// Only a person's own household may fetch their QR code.
/// </summary>
[ApiController]
public sealed class PersonQrController : ControllerBase
{
    [HttpGet("/persons/qr/{userId}")]
    public async Task<IResult> Get(
        string userId,
        [FromServices] AppDbContext db,
        CancellationToken cancellationToken)
    {
        var householdRaw = User.FindFirstValue(HouseholdClaims.HouseholdId);
        if (!int.TryParse(householdRaw, out var householdId))
        {
            return Results.Unauthorized();
        }

        var person = await db.Users
            .AsNoTracking()
            .Where(x => x.Id == userId && x.HouseholdId == householdId)
            .Select(x => new { x.PersonLinkToken })
            .FirstOrDefaultAsync(cancellationToken);

        if (person is null)
        {
            return Results.NotFound();
        }

        var url = $"{Request.Scheme}://{Request.Host}/persons/link/{person.PersonLinkToken}";

        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(url, QRCodeGenerator.ECCLevel.Q);
        var png = new PngByteQRCode(data).GetGraphic(10);

        return Results.File(png, "image/png");
    }
}
