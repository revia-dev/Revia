using Microsoft.EntityFrameworkCore;
using Revia.Data;

namespace Revia.Services;

/// <summary>Creates and queries links between households.</summary>
public sealed class HouseholdLinkService(AppDbContext db)
{
    private readonly AppDbContext _db = db;

    /// <summary>Returns the ids of households linked to the given one (excluding itself).</summary>
    public async Task<IReadOnlyList<int>> GetLinkedHouseholdIdsAsync(int householdId, CancellationToken cancellationToken = default)
    {
        return await _db.HouseholdLinks
            .AsNoTracking()
            .Where(x => x.HouseholdAId == householdId || x.HouseholdBId == householdId)
            .Select(x => x.HouseholdAId == householdId ? x.HouseholdBId : x.HouseholdAId)
            .Distinct()
            .ToListAsync(cancellationToken);
    }

    /// <summary>The given household plus every household linked to it.</summary>
    public async Task<IReadOnlyList<int>> GetVisibleHouseholdIdsAsync(int householdId, CancellationToken cancellationToken = default)
    {
        var linked = await GetLinkedHouseholdIdsAsync(householdId, cancellationToken);
        return linked.Append(householdId).Distinct().ToList();
    }

    /// <summary>Creates a link between two households if it does not exist yet.</summary>
    public async Task LinkAsync(int householdX, int householdY, CancellationToken cancellationToken = default)
    {
        if (householdX == householdY)
        {
            return;
        }

        var (a, b) = HouseholdLink.Normalize(householdX, householdY);
        var exists = await _db.HouseholdLinks.AnyAsync(x => x.HouseholdAId == a && x.HouseholdBId == b, cancellationToken);
        if (exists)
        {
            return;
        }

        _db.HouseholdLinks.Add(new HouseholdLink
        {
            HouseholdAId = a,
            HouseholdBId = b,
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(cancellationToken);
    }
}
