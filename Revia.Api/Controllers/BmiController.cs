using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Revia.Api.Auth;
using Revia.Data;
using Revia.Modules.Bmi;
using Revia.Services;
using Revia.Shared.Bmi;

namespace Revia.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/bmi")]
[ModuleAccess(AppModules.Bmi)]
public sealed class BmiController(
    BmiService bmi,
    AppDbContext db,
    IBmiPhotoStorage photoStorage,
    CurrentUserContext currentUser) : ControllerBase
{
    private readonly BmiService _bmi = bmi;
    private readonly AppDbContext _db = db;
    private readonly IBmiPhotoStorage _photoStorage = photoStorage;
    private readonly CurrentUserContext _currentUser = currentUser;

    [HttpGet]
    public async Task<ActionResult<List<BmiRecordDto>>> GetAll([FromQuery] string? personId, CancellationToken ct)
        => (await _bmi.GetAllAsync(personId, ct)).Select(ToDto).ToList();

    [HttpPost]
    public async Task<ActionResult<BmiRecordDto>> Add([FromBody] AddBmiRecordRequest request, CancellationToken ct)
    {
        Stream? photoStream = null;
        if (!string.IsNullOrEmpty(request.PhotoBase64) && !string.IsNullOrEmpty(request.PhotoContentType))
        {
            try
            {
                var bytes = Convert.FromBase64String(request.PhotoBase64);
                photoStream = new MemoryStream(bytes);
            }
            catch (FormatException)
            {
                return BadRequest(new { message = "Ungültige Foto-Daten." });
            }
        }

        try
        {
            var record = await _bmi.AddAsync(
                request.PersonId,
                request.MeasuredOn == default ? DateTime.Today : request.MeasuredOn,
                request.WeightKg,
                request.HeightCm,
                photoStream,
                request.PhotoContentType,
                request.PhotoName,
                ct);
            return ToDto(record);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        finally
        {
            if (photoStream is not null) await photoStream.DisposeAsync();
        }
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
        => await _bmi.DeleteAsync(id, ct) ? NoContent() : NotFound();

    /// <summary>Streams a measurement's snapshot photo (household-scoped, authenticated).
    /// Photos load when online; offline photo viewing is a later hardening step.</summary>
    [HttpGet("{id:int}/photo")]
    public async Task<IActionResult> Photo(int id, CancellationToken ct)
    {
        var householdId = await _currentUser.RequireHouseholdIdAsync();
        var record = await _db.BmiRecords.AsNoTracking()
            .Where(x => x.Id == id && x.HouseholdId == householdId)
            .Select(x => new { x.PhotoFileName })
            .FirstOrDefaultAsync(ct);

        if (record?.PhotoFileName is null)
        {
            return NotFound();
        }

        var path = _photoStorage.GetPath(record.PhotoFileName);
        if (!System.IO.File.Exists(path))
        {
            return NotFound();
        }

        var contentType = Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            ".gif" => "image/gif",
            _ => "application/octet-stream"
        };
        return PhysicalFile(path, contentType);
    }

    private static BmiRecordDto ToDto(BmiRecord r) => new()
    {
        Id = r.Id,
        PersonId = r.PersonId,
        PersonDisplayName = r.Person?.DisplayName ?? string.Empty,
        MeasuredOn = r.MeasuredOn,
        WeightKg = r.WeightKg,
        HeightCm = r.HeightCm,
        Bmi = r.Bmi,
        HasPhoto = r.PhotoFileName is not null,
        PhotoUrl = r.PhotoFileName is not null ? $"api/bmi/{r.Id}/photo" : null
    };
}
