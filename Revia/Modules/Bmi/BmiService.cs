using Microsoft.EntityFrameworkCore;
using Revia.Data;
using Revia.Services;

namespace Revia.Modules.Bmi;

/// <summary>Household-scoped CRUD for BMI measurements and their snapshot photos.</summary>
public sealed class BmiService(AppDbContext db, CurrentUserContext currentUser, IBmiPhotoStorage photoStorage)
{
    private readonly AppDbContext _db = db;
    private readonly CurrentUserContext _currentUser = currentUser;
    private readonly IBmiPhotoStorage _photoStorage = photoStorage;

    public async Task<IReadOnlyList<BmiRecord>> GetAllAsync(string? personId = null, CancellationToken cancellationToken = default)
    {
        var householdId = await _currentUser.RequireHouseholdIdAsync();
        var query = _db.BmiRecords
            .AsNoTracking()
            .Where(x => x.HouseholdId == householdId)
            .Include(x => x.Person)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(personId))
        {
            query = query.Where(x => x.PersonId == personId);
        }

        return await query
            .OrderBy(x => x.MeasuredOn)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<BmiRecord> AddAsync(
        string personId,
        DateTime measuredOn,
        double weightKg,
        double heightCm,
        Stream? photo,
        string? photoContentType,
        string? photoName,
        CancellationToken cancellationToken = default)
    {
        var householdId = await _currentUser.RequireHouseholdIdAsync();

        var personExists = await _db.Users.AnyAsync(x => x.Id == personId && x.HouseholdId == householdId, cancellationToken);
        if (!personExists)
        {
            throw new InvalidOperationException("Die ausgewählte Person gehört nicht zu diesem Haushalt.");
        }

        string? fileName = null;
        if (photo is not null && !string.IsNullOrWhiteSpace(photoContentType))
        {
            var ext = ExtensionForContentType(photoContentType, photoName);
            fileName = await _photoStorage.SaveAsync(photo, ext, cancellationToken);
        }

        var record = new BmiRecord
        {
            HouseholdId = householdId,
            PersonId = personId,
            MeasuredOn = measuredOn.Date,
            WeightKg = weightKg,
            HeightCm = heightCm,
            Bmi = BmiRecord.Calculate(weightKg, heightCm),
            PhotoFileName = fileName,
            CreatedAt = DateTime.UtcNow
        };

        try
        {
            _db.BmiRecords.Add(record);
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            if (fileName is not null) _photoStorage.Delete(fileName);
            throw;
        }

        return record;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var householdId = await _currentUser.RequireHouseholdIdAsync();
        var record = await _db.BmiRecords.FirstOrDefaultAsync(x => x.Id == id && x.HouseholdId == householdId, cancellationToken);
        if (record is null)
        {
            return false;
        }

        var fileName = record.PhotoFileName;
        _db.BmiRecords.Remove(record);
        await _db.SaveChangesAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(fileName))
        {
            _photoStorage.Delete(fileName);
        }

        return true;
    }

    private static string ExtensionForContentType(string contentType, string? originalName)
    {
        var ext = contentType.ToLowerInvariant() switch
        {
            "image/jpeg" or "image/jpg" => ".jpg",
            "image/png" => ".png",
            "image/webp" => ".webp",
            "image/gif" => ".gif",
            "image/heic" => ".heic",
            "image/heif" => ".heif",
            _ => null
        };

        if (ext is not null) return ext;

        var fromName = Path.GetExtension(originalName ?? string.Empty);
        return string.IsNullOrWhiteSpace(fromName) ? ".img" : fromName.ToLowerInvariant();
    }
}
