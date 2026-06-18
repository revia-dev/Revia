using Microsoft.EntityFrameworkCore;
using PrivateLibrary.Data;
using PrivateLibrary.Services;

namespace PrivateLibrary.Modules.Games;

/// <summary>One participant's input when creating/updating a match.</summary>
public sealed record MatchParticipantInput(string PersonId, double? Score);

/// <summary>Household-scoped CRUD for games, types, score definitions, expansions (with files), matches and files.</summary>
public sealed class GameService(AppDbContext db, CurrentUserContext currentUser, IGameFileStorage storage)
{
    private readonly AppDbContext _db = db;
    private readonly CurrentUserContext _currentUser = currentUser;
    private readonly IGameFileStorage _storage = storage;

    // ---- Games ----

    public async Task<IReadOnlyList<Game>> GetGamesAsync(string? search = null, CancellationToken cancellationToken = default)
    {
        var householdId = await _currentUser.RequireHouseholdIdAsync();
        var query = _db.Games.AsNoTracking().Where(x => x.HouseholdId == householdId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            query = query.Where(x => x.Name.Contains(s) || (x.Publisher != null && x.Publisher.Contains(s)));
        }

        return await query.Include(x => x.Types).OrderBy(x => x.Name).ToListAsync(cancellationToken);
    }

    public async Task<Game?> GetGameAsync(int id, CancellationToken cancellationToken = default)
    {
        var householdId = await _currentUser.RequireHouseholdIdAsync();
        return await _db.Games
            .Include(x => x.Types)
            .Include(x => x.ScoreDefinitions)
            .Include(x => x.Expansions.OrderBy(e => e.Name)).ThenInclude(e => e.Files)
            .FirstOrDefaultAsync(x => x.Id == id && x.HouseholdId == householdId, cancellationToken);
    }

    public async Task<Game> CreateGameAsync(Game game, IEnumerable<GameType> types, IEnumerable<GameScoreDefinition> scoreDefinitions, CancellationToken cancellationToken = default)
    {
        game.HouseholdId = await _currentUser.RequireHouseholdIdAsync();
        game.CreatedAt = DateTime.UtcNow;
        game.Types = BuildTypes(types);
        game.ScoreDefinitions = BuildScoreDefinitions(scoreDefinitions);
        _db.Games.Add(game);
        await _db.SaveChangesAsync(cancellationToken);
        return game;
    }

    public async Task<Game?> UpdateGameAsync(Game game, IEnumerable<GameType> types, IEnumerable<GameScoreDefinition> scoreDefinitions, CancellationToken cancellationToken = default)
    {
        var householdId = await _currentUser.RequireHouseholdIdAsync();
        var existing = await _db.Games
            .Include(x => x.Types)
            .Include(x => x.ScoreDefinitions)
            .FirstOrDefaultAsync(x => x.Id == game.Id && x.HouseholdId == householdId, cancellationToken);
        if (existing is null) return null;

        existing.Name = game.Name;
        existing.Publisher = game.Publisher;
        existing.ReleaseYear = game.ReleaseYear;
        existing.MinPlayers = game.MinPlayers;
        existing.MaxPlayers = game.MaxPlayers;
        existing.Description = game.Description;

        existing.Types.Clear();
        foreach (var t in BuildTypes(types)) existing.Types.Add(t);

        // Reconcile score definitions: keep existing by id, add new, remove dropped.
        var desired = scoreDefinitions.Where(d => !string.IsNullOrWhiteSpace(d.Name)).ToList();
        var keepIds = desired.Where(d => d.Id != 0).Select(d => d.Id).ToHashSet();
        foreach (var toRemove in existing.ScoreDefinitions.Where(d => !keepIds.Contains(d.Id)).ToList())
        {
            existing.ScoreDefinitions.Remove(toRemove);
        }
        foreach (var d in desired)
        {
            var current = existing.ScoreDefinitions.FirstOrDefault(x => x.Id == d.Id && d.Id != 0);
            if (current is null)
            {
                existing.ScoreDefinitions.Add(new GameScoreDefinition { Name = d.Name.Trim(), Unit = d.Unit?.Trim(), HigherIsBetter = d.HigherIsBetter });
            }
            else
            {
                current.Name = d.Name.Trim();
                current.Unit = d.Unit?.Trim();
                current.HigherIsBetter = d.HigherIsBetter;
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
        return existing;
    }

    public async Task<bool> DeleteGameAsync(int id, CancellationToken cancellationToken = default)
    {
        var householdId = await _currentUser.RequireHouseholdIdAsync();
        var game = await _db.Games
            .Include(x => x.Matches).ThenInclude(m => m.Photos)
            .Include(x => x.Expansions).ThenInclude(e => e.Files)
            .FirstOrDefaultAsync(x => x.Id == id && x.HouseholdId == householdId, cancellationToken);
        if (game is null) return false;

        var files = game.Matches.SelectMany(m => m.Photos.Select(p => p.FileName)).ToList();
        files.AddRange(game.Expansions.SelectMany(e => e.Files.Select(f => f.FileName)));
        if (!string.IsNullOrWhiteSpace(game.RulesFileName)) files.Add(game.RulesFileName);

        _db.Games.Remove(game);
        await _db.SaveChangesAsync(cancellationToken);

        foreach (var file in files) _storage.Delete(file);
        return true;
    }

    public async Task SetRulesAsync(int gameId, Stream content, string contentType, string? originalName, CancellationToken cancellationToken = default)
    {
        var householdId = await _currentUser.RequireHouseholdIdAsync();
        var game = await _db.Games.FirstOrDefaultAsync(x => x.Id == gameId && x.HouseholdId == householdId, cancellationToken)
            ?? throw new InvalidOperationException("Spiel nicht gefunden.");

        var ext = ExtensionForContentType(contentType, originalName);
        var fileName = await _storage.SaveAsync(content, ext, cancellationToken);
        var previous = game.RulesFileName;

        game.RulesFileName = fileName;
        game.RulesContentType = contentType;
        game.RulesOriginalName = originalName;
        await _db.SaveChangesAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(previous)) _storage.Delete(previous);
    }

    // ---- Expansions ----

    public async Task<int> AddExpansionAsync(int gameId, string name, string? notes, CancellationToken cancellationToken = default)
    {
        var householdId = await _currentUser.RequireHouseholdIdAsync();
        var owns = await _db.Games.AnyAsync(x => x.Id == gameId && x.HouseholdId == householdId, cancellationToken);
        if (!owns) throw new InvalidOperationException("Spiel nicht gefunden.");

        var expansion = new GameExpansion
        {
            GameId = gameId,
            Name = name.Trim(),
            Notes = notes?.Trim(),
            CreatedAt = DateTime.UtcNow
        };
        _db.GameExpansions.Add(expansion);
        await _db.SaveChangesAsync(cancellationToken);
        return expansion.Id;
    }

    public async Task DeleteExpansionAsync(int expansionId, CancellationToken cancellationToken = default)
    {
        var householdId = await _currentUser.RequireHouseholdIdAsync();
        var expansion = await _db.GameExpansions
            .Include(x => x.Files)
            .FirstOrDefaultAsync(x => x.Id == expansionId && x.Game!.HouseholdId == householdId, cancellationToken);
        if (expansion is null) return;

        var files = expansion.Files.Select(f => f.FileName).ToList();
        _db.GameExpansions.Remove(expansion);
        await _db.SaveChangesAsync(cancellationToken);
        foreach (var f in files) _storage.Delete(f);
    }

    public async Task AddExpansionFileAsync(int expansionId, Stream content, string contentType, string? originalName, CancellationToken cancellationToken = default)
    {
        var householdId = await _currentUser.RequireHouseholdIdAsync();
        var expansion = await _db.GameExpansions
            .FirstOrDefaultAsync(x => x.Id == expansionId && x.Game!.HouseholdId == householdId, cancellationToken)
            ?? throw new InvalidOperationException("Erweiterung nicht gefunden.");

        var ext = ExtensionForContentType(contentType, originalName);
        var fileName = await _storage.SaveAsync(content, ext, cancellationToken);
        try
        {
            _db.GameExpansionFiles.Add(new GameExpansionFile
            {
                GameExpansionId = expansion.Id,
                FileName = fileName,
                ContentType = contentType,
                OriginalName = originalName,
                CreatedAt = DateTime.UtcNow
            });
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            _storage.Delete(fileName);
            throw;
        }
    }

    public async Task RemoveExpansionFileAsync(int fileId, CancellationToken cancellationToken = default)
    {
        var householdId = await _currentUser.RequireHouseholdIdAsync();
        var file = await _db.GameExpansionFiles
            .FirstOrDefaultAsync(x => x.Id == fileId && x.GameExpansion!.Game!.HouseholdId == householdId, cancellationToken);
        if (file is null) return;

        _db.GameExpansionFiles.Remove(file);
        await _db.SaveChangesAsync(cancellationToken);
        _storage.Delete(file.FileName);
    }

    // ---- Matches ----

    public async Task<IReadOnlyList<GameMatch>> GetRecentMatchesAsync(int count = 5, CancellationToken cancellationToken = default)
    {
        var householdId = await _currentUser.RequireHouseholdIdAsync();
        return await _db.GameMatches
            .AsNoTracking()
            .Where(x => x.HouseholdId == householdId)
            .Include(x => x.Game)
            .Include(x => x.Participants).ThenInclude(p => p.Person)
            .OrderByDescending(x => x.StartedAt)
            .Take(count)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<GameMatch>> GetMatchesForGameAsync(int gameId, string? search = null, CancellationToken cancellationToken = default)
    {
        var householdId = await _currentUser.RequireHouseholdIdAsync();
        var query = _db.GameMatches
            .AsNoTracking()
            .Where(x => x.GameId == gameId && x.HouseholdId == householdId)
            .Include(x => x.Participants).ThenInclude(p => p.Person)
            .Include(x => x.Photos.OrderBy(p => p.SortOrder).ThenBy(p => p.Id))
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            query = query.Where(x =>
                (x.Title != null && x.Title.Contains(s)) ||
                (x.Notes != null && x.Notes.Contains(s)) ||
                x.Participants.Any(p =>
                    (p.Person!.FirstName + " " + p.Person.LastName).Contains(s) ||
                    (p.Person.Nickname != null && p.Person.Nickname.Contains(s))));
        }

        return await query.OrderByDescending(x => x.PlayedOn).ThenByDescending(x => x.Id).ToListAsync(cancellationToken);
    }

    public async Task<GameMatch?> GetMatchAsync(int id, CancellationToken cancellationToken = default)
    {
        var householdId = await _currentUser.RequireHouseholdIdAsync();
        return await _db.GameMatches
            .Include(x => x.Game).ThenInclude(g => g!.ScoreDefinitions)
            .Include(x => x.ScoreDefinition)
            .Include(x => x.Photos.OrderBy(p => p.SortOrder).ThenBy(p => p.Id))
            .Include(x => x.Participants).ThenInclude(p => p.Person)
            .FirstOrDefaultAsync(x => x.Id == id && x.HouseholdId == householdId, cancellationToken);
    }

    public async Task<GameMatch> CreateMatchAsync(int gameId, GameMatch match, IEnumerable<MatchParticipantInput> participants, CancellationToken cancellationToken = default)
    {
        var householdId = await _currentUser.RequireHouseholdIdAsync();
        var owns = await _db.Games.AnyAsync(x => x.Id == gameId && x.HouseholdId == householdId, cancellationToken);
        if (!owns) throw new InvalidOperationException("Spiel nicht gefunden.");

        match.GameId = gameId;
        match.HouseholdId = householdId;
        match.StartedAt = DateTime.UtcNow;
        if (match.PlayedOn == default) match.PlayedOn = DateTime.Today;
        match.Participants = BuildParticipants(participants);

        _db.GameMatches.Add(match);
        await _db.SaveChangesAsync(cancellationToken);
        return match;
    }

    public async Task<GameMatch?> UpdateMatchAsync(GameMatch match, IEnumerable<MatchParticipantInput> participants, CancellationToken cancellationToken = default)
    {
        var householdId = await _currentUser.RequireHouseholdIdAsync();
        var existing = await _db.GameMatches
            .Include(x => x.Participants)
            .FirstOrDefaultAsync(x => x.Id == match.Id && x.HouseholdId == householdId, cancellationToken);
        if (existing is null) return null;
        if (existing.IsCompleted) throw new InvalidOperationException("Abgeschlossene Partien können nicht bearbeitet werden.");

        existing.Title = match.Title;
        existing.PlayedOn = match.PlayedOn;
        existing.ScoreDefinitionId = match.ScoreDefinitionId;
        existing.Notes = match.Notes;

        existing.Participants.Clear();
        foreach (var p in BuildParticipants(participants)) existing.Participants.Add(p);

        await _db.SaveChangesAsync(cancellationToken);
        return existing;
    }

    public async Task SetMatchCompletedAsync(int id, bool completed, CancellationToken cancellationToken = default)
    {
        var householdId = await _currentUser.RequireHouseholdIdAsync();
        var match = await _db.GameMatches.FirstOrDefaultAsync(x => x.Id == id && x.HouseholdId == householdId, cancellationToken);
        if (match is null) return;
        match.IsCompleted = completed;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> DeleteMatchAsync(int id, CancellationToken cancellationToken = default)
    {
        var householdId = await _currentUser.RequireHouseholdIdAsync();
        var match = await _db.GameMatches
            .Include(x => x.Photos)
            .FirstOrDefaultAsync(x => x.Id == id && x.HouseholdId == householdId, cancellationToken);
        if (match is null) return false;

        var files = match.Photos.Select(p => p.FileName).ToList();
        _db.GameMatches.Remove(match);
        await _db.SaveChangesAsync(cancellationToken);
        foreach (var file in files) _storage.Delete(file);
        return true;
    }

    public async Task AddMatchPhotoAsync(int matchId, Stream content, string contentType, string? originalName, CancellationToken cancellationToken = default)
    {
        var householdId = await _currentUser.RequireHouseholdIdAsync();
        var match = await _db.GameMatches.FirstOrDefaultAsync(x => x.Id == matchId && x.HouseholdId == householdId, cancellationToken)
            ?? throw new InvalidOperationException("Partie nicht gefunden.");

        var ext = ExtensionForContentType(contentType, originalName);
        var fileName = await _storage.SaveAsync(content, ext, cancellationToken);
        var next = await _db.GameMatchPhotos.Where(x => x.GameMatchId == matchId).Select(x => (int?)x.SortOrder).MaxAsync(cancellationToken) ?? -1;

        try
        {
            _db.GameMatchPhotos.Add(new GameMatchPhoto
            {
                GameMatchId = matchId,
                FileName = fileName,
                ContentType = contentType,
                OriginalName = originalName,
                SortOrder = next + 1,
                CreatedAt = DateTime.UtcNow
            });
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            _storage.Delete(fileName);
            throw;
        }
    }

    public async Task RemoveMatchPhotoAsync(int photoId, CancellationToken cancellationToken = default)
    {
        var householdId = await _currentUser.RequireHouseholdIdAsync();
        var photo = await _db.GameMatchPhotos
            .FirstOrDefaultAsync(x => x.Id == photoId && x.GameMatch!.HouseholdId == householdId, cancellationToken);
        if (photo is null) return;

        _db.GameMatchPhotos.Remove(photo);
        await _db.SaveChangesAsync(cancellationToken);
        _storage.Delete(photo.FileName);
    }

    private static List<GameTypeAssignment> BuildTypes(IEnumerable<GameType> types) =>
        types.Distinct().Select(t => new GameTypeAssignment { Type = t }).ToList();

    private static List<GameScoreDefinition> BuildScoreDefinitions(IEnumerable<GameScoreDefinition> defs) =>
        defs.Where(d => !string.IsNullOrWhiteSpace(d.Name))
            .Select(d => new GameScoreDefinition { Name = d.Name.Trim(), Unit = d.Unit?.Trim(), HigherIsBetter = d.HigherIsBetter })
            .ToList();

    private static List<GameMatchParticipant> BuildParticipants(IEnumerable<MatchParticipantInput> participants) =>
        participants.Where(p => !string.IsNullOrWhiteSpace(p.PersonId))
            .GroupBy(p => p.PersonId)
            .Select(g => new GameMatchParticipant { PersonId = g.Key, Score = g.First().Score })
            .ToList();

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
            "application/pdf" => ".pdf",
            _ => null
        };

        if (ext is not null) return ext;

        var fromName = Path.GetExtension(originalName ?? string.Empty);
        return string.IsNullOrWhiteSpace(fromName) ? ".img" : fromName.ToLowerInvariant();
    }
}
