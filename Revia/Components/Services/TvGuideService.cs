using Microsoft.EntityFrameworkCore;
using Revia.Data;

namespace Revia.Services;

/// <summary>Lightweight movie option for the program picker.</summary>
public sealed record MovieOption(Guid Id, string Title, string? Year);

/// <summary>
/// Manages the per-member private "TV guide" and the shared household program.
/// All access is scoped to the current household / current user.
/// </summary>
public sealed class TvGuideService(AppDbContext db, CurrentUserContext currentUser)
{
    private readonly AppDbContext _db = db;
    private readonly CurrentUserContext _currentUser = currentUser;

    public async Task<TvProgram> GetPrivateProgramAsync(CancellationToken cancellationToken = default)
    {
        var householdId = await _currentUser.RequireHouseholdIdAsync();
        var userId = await RequireUserIdAsync();

        var program = await _db.TvPrograms
            .FirstOrDefaultAsync(x => x.HouseholdId == householdId && x.OwnerUserId == userId, cancellationToken);

        if (program is null)
        {
            program = new TvProgram { HouseholdId = householdId, OwnerUserId = userId, Name = "Mein Programm" };
            _db.TvPrograms.Add(program);
            await _db.SaveChangesAsync(cancellationToken);
        }

        return program;
    }

    public async Task<TvProgram> GetHouseholdProgramAsync(CancellationToken cancellationToken = default)
    {
        var householdId = await _currentUser.RequireHouseholdIdAsync();

        var program = await _db.TvPrograms
            .FirstOrDefaultAsync(x => x.HouseholdId == householdId && x.OwnerUserId == null, cancellationToken);

        if (program is null)
        {
            program = new TvProgram { HouseholdId = householdId, OwnerUserId = null, Name = "Familienprogramm" };
            _db.TvPrograms.Add(program);
            await _db.SaveChangesAsync(cancellationToken);
        }

        return program;
    }

    public async Task<IReadOnlyList<TvProgramEntry>> GetPrivateEntriesAsync(CancellationToken cancellationToken = default)
    {
        var program = await GetPrivateProgramAsync(cancellationToken);
        return await _db.TvProgramEntries
            .AsNoTracking()
            .Where(x => x.TvProgramId == program.Id)
            .Include(x => x.Movie)
            .OrderBy(x => x.StartsAt)
            .ToListAsync(cancellationToken);
    }

    /// <summary>The household program's own entries plus every member's private entries
    /// that were shared with the household, sorted into one linear schedule.</summary>
    public async Task<IReadOnlyList<TvProgramEntry>> GetHouseholdEntriesAsync(CancellationToken cancellationToken = default)
    {
        var householdId = await _currentUser.RequireHouseholdIdAsync();

        return await _db.TvProgramEntries
            .AsNoTracking()
            .Where(x => x.TvProgram!.HouseholdId == householdId
                        && (x.TvProgram.OwnerUserId == null || x.SharedWithHousehold))
            .Include(x => x.Movie)
            .Include(x => x.TvProgram!).ThenInclude(p => p.OwnerUser)
            .OrderBy(x => x.StartsAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<MovieOption>> GetMovieOptionsAsync(CancellationToken cancellationToken = default)
    {
        var householdId = await _currentUser.RequireHouseholdIdAsync();
        return await _db.Movies
            .AsNoTracking()
            .Where(x => x.HouseholdId == householdId)
            .OrderBy(x => x.Titel)
            .Select(x => new MovieOption(x.Id, x.Titel ?? "Unbenannt", x.Jahr))
            .ToListAsync(cancellationToken);
    }

    public async Task AddPrivateEntryAsync(Guid? movieId, string? customTitle, DateTime startsAt, string? channel, string? note, bool shared, CancellationToken cancellationToken = default)
    {
        var program = await GetPrivateProgramAsync(cancellationToken);
        var (resolvedMovieId, resolvedTitle) = await ResolveEntryTargetAsync(movieId, customTitle, cancellationToken);

        _db.TvProgramEntries.Add(new TvProgramEntry
        {
            TvProgramId = program.Id,
            MovieId = resolvedMovieId,
            CustomTitle = resolvedTitle,
            StartsAt = startsAt,
            Channel = channel?.Trim(),
            Note = note?.Trim(),
            SharedWithHousehold = shared
        });
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task AddHouseholdEntryAsync(Guid? movieId, string? customTitle, DateTime startsAt, string? channel, string? note, CancellationToken cancellationToken = default)
    {
        var program = await GetHouseholdProgramAsync(cancellationToken);
        var (resolvedMovieId, resolvedTitle) = await ResolveEntryTargetAsync(movieId, customTitle, cancellationToken);

        _db.TvProgramEntries.Add(new TvProgramEntry
        {
            TvProgramId = program.Id,
            MovieId = resolvedMovieId,
            CustomTitle = resolvedTitle,
            StartsAt = startsAt,
            Channel = channel?.Trim(),
            Note = note?.Trim()
        });
        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>An entry is either tied to a household movie OR carries a custom title.</summary>
    private async Task<(Guid? movieId, string? customTitle)> ResolveEntryTargetAsync(
        Guid? movieId, string? customTitle, CancellationToken cancellationToken)
    {
        if (movieId is { } id && id != Guid.Empty)
        {
            await EnsureMovieInHouseholdAsync(id, cancellationToken);
            return (id, null);
        }
        var title = customTitle?.Trim();
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new InvalidOperationException("Entweder ein Film oder ein eigener Titel ist erforderlich.");
        }
        return (null, title);
    }

    public async Task<bool> DeleteEntryAsync(int entryId, CancellationToken cancellationToken = default)
    {
        var entry = await LoadEditableEntryAsync(entryId, cancellationToken);
        if (entry is null)
        {
            return false;
        }

        _db.TvProgramEntries.Remove(entry);
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> SetSharedAsync(int entryId, bool shared, CancellationToken cancellationToken = default)
    {
        var entry = await LoadEditableEntryAsync(entryId, cancellationToken);
        if (entry?.TvProgram?.OwnerUserId is null)
        {
            return false; // sharing only applies to private entries
        }

        entry.SharedWithHousehold = shared;
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task<TvProgramEntry?> LoadEditableEntryAsync(int entryId, CancellationToken cancellationToken)
    {
        var householdId = await _currentUser.RequireHouseholdIdAsync();
        var userId = await RequireUserIdAsync();

        var entry = await _db.TvProgramEntries
            .Include(x => x.TvProgram)
            .FirstOrDefaultAsync(x => x.Id == entryId && x.TvProgram!.HouseholdId == householdId, cancellationToken);

        if (entry?.TvProgram is null)
        {
            return null;
        }

        // A member may edit the household program and their own private program.
        var isOwn = entry.TvProgram.OwnerUserId == null || entry.TvProgram.OwnerUserId == userId;
        return isOwn ? entry : null;
    }

    private async Task EnsureMovieInHouseholdAsync(Guid movieId, CancellationToken cancellationToken)
    {
        var householdId = await _currentUser.RequireHouseholdIdAsync();
        var exists = await _db.Movies.AnyAsync(x => x.Id == movieId && x.HouseholdId == householdId, cancellationToken);
        if (!exists)
        {
            throw new InvalidOperationException("Der ausgewählte Film gehört nicht zu diesem Haushalt.");
        }
    }

    private async Task<string> RequireUserIdAsync()
    {
        return await _currentUser.GetUserIdAsync()
            ?? throw new InvalidOperationException("Kein angemeldeter Benutzer.");
    }
}
