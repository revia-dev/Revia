using Microsoft.EntityFrameworkCore;
using Revia.Data;
using Revia.Modules.Bmi;
using Revia.Modules.Lending.Models;
using Revia.Services;
using Revia.Shared.Backup;

namespace Revia.Api.Backup;

/// <summary>
/// Produces/consumes the cross-app <see cref="BackupDocument"/> for the signed-in
/// user's household. Maps the web's <see cref="Movie"/> entity (German columns,
/// comma-joined lists, '#'-tag series) to/from the shared, English, camelCase JSON the
/// native app reads, plus the per-person watch progress and BMI tables — the latter
/// reconcile by <c>personName</c> (the web's Identity ids ≠ the native UUIDs).
/// </summary>
public sealed class BackupService(AppDbContext db, CurrentUserContext currentUser, IBmiPhotoStorage photoStorage)
{
    private readonly AppDbContext _db = db;
    private readonly CurrentUserContext _currentUser = currentUser;
    private readonly IBmiPhotoStorage _photoStorage = photoStorage;

    public async Task<BackupDocument> ExportAsync(CancellationToken ct = default)
    {
        var householdId = await _currentUser.RequireHouseholdIdAsync();

        var movies = await _db.Movies.AsNoTracking()
            .Where(m => m.HouseholdId == householdId).ToListAsync(ct);

        var users = await _db.Users.AsNoTracking()
            .Where(u => u.HouseholdId == householdId).ToListAsync(ct);
        var nameOf = users.ToDictionary(u => u.Id, u => u.DisplayName);

        var progress = await _db.WatchProgresses.AsNoTracking()
            .Where(w => w.HouseholdId == householdId).ToListAsync(ct);
        var bmis = await _db.BmiRecords.AsNoTracking()
            .Where(b => b.HouseholdId == householdId).ToListAsync(ct);
        var loans = await _db.LendingEntries.AsNoTracking().Include(l => l.Items)
            .Where(l => l.HouseholdId == householdId).ToListAsync(ct);
        var matches = await _db.GameMatches.AsNoTracking()
            .Include(m => m.Game).Include(m => m.Participants)
            .Where(m => m.HouseholdId == householdId).ToListAsync(ct);
        var wishlists = await _db.Wishlists.AsNoTracking().Include(w => w.Items)
            .Where(w => w.HouseholdId == householdId).ToListAsync(ct);
        var tvPrograms = await _db.TvPrograms.AsNoTracking().Include(p => p.Entries)
            .Where(p => p.HouseholdId == householdId).ToListAsync(ct);

        return new BackupDocument
        {
            Movies = movies.Select(ToBak).ToList(),
            WatchProgress = progress.Select(w => new WatchProgressBak
            {
                Id = w.Id,
                PersonId = Guid.TryParse(w.UserId, out var g) ? g : Guid.Empty,
                PersonName = nameOf.GetValueOrDefault(w.UserId, string.Empty),
                MovieId = w.MovieId,
                Status = w.Status,
                PositionSeconds = w.PositionSeconds,
                Season = w.Season,
                Episode = w.Episode,
                UpdatedAt = w.UpdatedAt,
            }).ToList(),
            BmiRecords = bmis.Select(b => new BmiBak
            {
                Id = Guid.NewGuid(), // web BMI ids are ints; the row matches by person+date
                PersonId = Guid.TryParse(b.PersonId, out var g) ? g : Guid.Empty,
                PersonName = nameOf.GetValueOrDefault(b.PersonId, string.Empty),
                MeasuredOn = b.MeasuredOn,
                WeightKg = b.WeightKg,
                HeightCm = b.HeightCm,
                PhotoData = ReadPhoto(b.PhotoFileName),
            }).ToList(),
            Loans = loans.Select(l => new LoanBak
            {
                Id = Guid.NewGuid(), // web lending ids are ints; the row carries its names
                Title = l.Title,
                Items = l.Items.OrderBy(i => i.SortOrder).Select(i => i.Name).ToList(),
                LenderPersonId = Guid.TryParse(l.LenderId, out var lg) ? lg : Guid.Empty,
                LenderName = nameOf.GetValueOrDefault(l.LenderId, string.Empty),
                BorrowerPersonId = Guid.TryParse(l.BorrowerId, out var bg) ? bg : Guid.Empty,
                BorrowerName = nameOf.GetValueOrDefault(l.BorrowerId, string.Empty),
                LoanDate = l.LoanDate,
                DueDate = l.DueDate,
                ReturnedAt = l.ReturnedAt,
                PhotoData = null, // loan photos are not bridged yet
            }).ToList(),
            Matches = matches.Select(m => new MatchBak
            {
                Id = Guid.NewGuid(),
                GameId = Guid.Empty, // games reconcile by name (web ids are ints)
                GameName = m.Game?.Name ?? string.Empty,
                PlayedOn = m.PlayedOn,
                Note = m.Notes,
                PhotoData = null,
                Participants = m.Participants.Select(p => new ParticipantBak
                {
                    Id = Guid.NewGuid(),
                    PersonId = Guid.TryParse(p.PersonId, out var pg) ? pg : Guid.Empty,
                    PersonName = nameOf.GetValueOrDefault(p.PersonId, string.Empty),
                    Score = p.Score ?? 0,
                }).ToList(),
            }).ToList(),
            Wishlists = wishlists.Select(w => new WishlistBak
            {
                Id = Guid.NewGuid(), // web wishlist ids are ints
                HouseholdId = Guid.Empty, // household is resolved locally on import
                Name = w.Name,
                CreatedAt = w.CreatedAt,
                OwnerName = nameOf.GetValueOrDefault(w.OwnerUserId, string.Empty),
                Items = w.Items.OrderBy(i => i.SortOrder).Select(i => new WishlistItemBak
                {
                    Id = Guid.NewGuid(),
                    Title = i.Title,
                    Note = i.Notes,
                    Url = i.Url,
                    PhotoData = null, // the web wishlist item has no photo
                    Fulfilled = i.IsFulfilled,
                }).ToList(),
            }).ToList(),
            TvAirings = tvPrograms.SelectMany(p => p.Entries.Select(e => new TvAiringBak
            {
                Id = Guid.NewGuid(),
                MovieId = e.MovieId,
                CustomTitle = null, // web entries always reference a movie
                PhotoData = null,
                Channel = e.Channel,
                Note = e.Note,
                StartsAt = e.StartsAt,
                Scope = e.SharedWithHousehold ? "family" : "privateProgram",
                OwnerPersonId = Guid.TryParse(p.OwnerUserId, out var og) ? og : null,
                OwnerName = p.OwnerUserId is null ? null : nameOf.GetValueOrDefault(p.OwnerUserId, string.Empty),
                BaseEpisode = null,
                ProgramName = p.Name,
            })).ToList(),
        };
    }

    /// <summary>Selective restore: overwrites only the tables present in the document.
    /// Returns the names of the tables that were overwritten.</summary>
    public async Task<List<string>> RestoreAsync(BackupDocument doc, CancellationToken ct = default)
    {
        var householdId = await _currentUser.RequireHouseholdIdAsync();
        var overwritten = new List<string>();

        if (doc.Movies is not null)
        {
            var existing = _db.Movies.Where(m => m.HouseholdId == householdId);
            _db.Movies.RemoveRange(existing);
            foreach (var bak in doc.Movies)
            {
                _db.Movies.Add(FromBak(bak, householdId));
            }
            overwritten.Add("movies");
        }

        // Person-keyed tables: resolve the owner from the household's users BY NAME so a
        // backup made on the native app (different ids) re-attaches to the right person.
        var users = await _db.Users.AsNoTracking()
            .Where(u => u.HouseholdId == householdId).ToListAsync(ct);

        if (doc.WatchProgress is not null)
        {
            _db.WatchProgresses.RemoveRange(
                _db.WatchProgresses.Where(w => w.HouseholdId == householdId));
            foreach (var b in doc.WatchProgress)
            {
                var userId = ResolveUserId(users, b.PersonName);
                if (userId is null) continue; // orphan — no local person of that name
                _db.WatchProgresses.Add(new WatchProgress
                {
                    Id = b.Id == Guid.Empty ? Guid.NewGuid() : b.Id,
                    HouseholdId = householdId,
                    UserId = userId,
                    MovieId = b.MovieId,
                    Status = b.Status,
                    PositionSeconds = b.PositionSeconds,
                    Season = b.Season,
                    Episode = b.Episode,
                    UpdatedAt = b.UpdatedAt == default ? DateTime.UtcNow : b.UpdatedAt,
                });
            }
            overwritten.Add("watchProgress");
        }

        if (doc.BmiRecords is not null)
        {
            _db.BmiRecords.RemoveRange(_db.BmiRecords.Where(b => b.HouseholdId == householdId));
            foreach (var b in doc.BmiRecords)
            {
                var userId = ResolveUserId(users, b.PersonName);
                if (userId is null) continue;
                string? fileName = null;
                if (b.PhotoData is { Length: > 0 })
                {
                    using var ms = new MemoryStream(b.PhotoData);
                    fileName = await _photoStorage.SaveAsync(ms, ".jpg", ct);
                }
                _db.BmiRecords.Add(new BmiRecord
                {
                    HouseholdId = householdId,
                    PersonId = userId,
                    MeasuredOn = b.MeasuredOn,
                    WeightKg = b.WeightKg,
                    HeightCm = b.HeightCm,
                    Bmi = BmiRecord.Calculate(b.WeightKg, b.HeightCm),
                    PhotoFileName = fileName,
                    CreatedAt = DateTime.UtcNow,
                });
            }
            overwritten.Add("bmiRecords");
        }

        if (doc.Loans is not null)
        {
            _db.LendingEntries.RemoveRange(_db.LendingEntries.Where(l => l.HouseholdId == householdId));
            foreach (var b in doc.Loans)
            {
                var lender = ResolveUserId(users, b.LenderName);
                var borrower = ResolveUserId(users, b.BorrowerName);
                if (lender is null || borrower is null) continue; // FK needs real users
                _db.LendingEntries.Add(new LendingEntry
                {
                    HouseholdId = householdId,
                    Title = b.Title ?? b.Items.FirstOrDefault(),
                    LenderId = lender,
                    BorrowerId = borrower,
                    LoanDate = b.LoanDate,
                    DueDate = b.DueDate,
                    ReturnedAt = b.ReturnedAt,
                    Status = b.ReturnedAt.HasValue
                        ? LendingEntryStatus.Returned
                        : (b.DueDate is { } due && due.Date < DateTime.Today
                            ? LendingEntryStatus.Overdue : LendingEntryStatus.Active),
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    Items = b.Items.Select((name, i) => new LendingItem { Name = name, SortOrder = i }).ToList(),
                });
            }
            overwritten.Add("loans");
        }

        if (doc.Matches is not null)
        {
            var games = await _db.Games.Where(g => g.HouseholdId == householdId).ToListAsync(ct);
            _db.GameMatches.RemoveRange(_db.GameMatches.Where(m => m.HouseholdId == householdId));
            foreach (var b in doc.Matches)
            {
                var game = games.FirstOrDefault(g =>
                    string.Equals(g.Name, b.GameName, StringComparison.OrdinalIgnoreCase));
                if (game is null) continue; // FK needs a known game
                _db.GameMatches.Add(new GameMatch
                {
                    GameId = game.Id,
                    HouseholdId = householdId,
                    PlayedOn = b.PlayedOn,
                    StartedAt = b.PlayedOn,
                    IsCompleted = true,
                    Notes = b.Note,
                    Participants = b.Participants
                        .Select(p => (UserId: ResolveUserId(users, p.PersonName), p.Score))
                        .Where(x => x.UserId is not null)
                        .Select(x => new GameMatchParticipant { PersonId = x.UserId!, Score = x.Score })
                        .ToList(),
                });
            }
            overwritten.Add("matches");
        }

        if (doc.Wishlists is not null)
        {
            // The native app keeps lists household-wide; on the web every list needs an
            // owner — resolve by name, else fall back to the importing admin.
            var importerId = await _currentUser.GetUserIdAsync();
            _db.Wishlists.RemoveRange(_db.Wishlists.Where(w => w.HouseholdId == householdId));
            foreach (var b in doc.Wishlists)
            {
                var ownerId = ResolveUserId(users, b.OwnerName ?? string.Empty) ?? importerId;
                if (ownerId is null) continue; // no usable owner
                _db.Wishlists.Add(new Wishlist
                {
                    HouseholdId = householdId,
                    OwnerUserId = ownerId,
                    Name = b.Name,
                    CreatedAt = b.CreatedAt == default ? DateTime.UtcNow : b.CreatedAt,
                    Items = b.Items.Select((i, idx) => new WishlistItem
                    {
                        Title = i.Title,
                        Url = i.Url,
                        Notes = i.Note,
                        IsFulfilled = i.Fulfilled,
                        SortOrder = idx,
                        CreatedAt = DateTime.UtcNow,
                    }).ToList(),
                });
            }
            overwritten.Add("wishlists");
        }

        if (doc.TvAirings is not null)
        {
            // Web entries need a known movie; rebuild programs by grouping flat airings
            // on programName (a nameless airing — e.g. from native — becomes its own
            // single-entry program).
            var importerId = await _currentUser.GetUserIdAsync();
            var movieIds = (await _db.Movies.Where(m => m.HouseholdId == householdId)
                .Select(m => m.Id).ToListAsync(ct)).ToHashSet();
            // include movies added earlier in THIS restore (same backup), not yet saved
            foreach (var added in _db.ChangeTracker.Entries<Movie>()
                .Where(e => e.State == EntityState.Added))
            {
                movieIds.Add(added.Entity.Id);
            }

            var oldProgramIds = await _db.TvPrograms.Where(p => p.HouseholdId == householdId)
                .Select(p => p.Id).ToListAsync(ct);
            _db.TvProgramEntries.RemoveRange(
                _db.TvProgramEntries.Where(e => oldProgramIds.Contains(e.TvProgramId)));
            _db.TvPrograms.RemoveRange(_db.TvPrograms.Where(p => p.HouseholdId == householdId));

            var groups = doc.TvAirings
                .Select((a, idx) => (a, key: string.IsNullOrWhiteSpace(a.ProgramName)
                    ? $"\0solo:{idx}" : a.ProgramName!))
                .GroupBy(x => x.key);
            foreach (var group in groups)
            {
                var first = group.First().a;
                var entries = group.Select(x => x.a)
                    .Where(a => a.MovieId is { } mid && mid != Guid.Empty && movieIds.Contains(mid))
                    .Select(a => new TvProgramEntry
                    {
                        MovieId = a.MovieId!.Value,
                        StartsAt = a.StartsAt,
                        Channel = a.Channel,
                        Note = a.Note,
                        SharedWithHousehold = a.Scope == "family",
                    }).ToList();
                if (entries.Count == 0) continue; // nothing the web can show
                _db.TvPrograms.Add(new TvProgram
                {
                    HouseholdId = householdId,
                    OwnerUserId = ResolveUserId(users, first.OwnerName ?? string.Empty) ?? importerId,
                    Name = first.ProgramName ?? first.CustomTitle ?? "TV",
                    Entries = entries,
                });
            }
            overwritten.Add("tvAirings");
        }

        await _db.SaveChangesAsync(ct);
        return overwritten;
    }

    /// <summary>Finds a household user whose name matches the backup's personName,
    /// trying the display name and its parts (case-insensitive). Null = no match.</summary>
    private static string? ResolveUserId(IReadOnlyList<ApplicationUser> users, string personName)
    {
        if (string.IsNullOrWhiteSpace(personName)) return null;
        var name = personName.Trim();
        bool Eq(string? s) => !string.IsNullOrWhiteSpace(s)
            && string.Equals(s.Trim(), name, StringComparison.OrdinalIgnoreCase);

        var match = users.FirstOrDefault(u => Eq(u.DisplayName))
            ?? users.FirstOrDefault(u => Eq(u.Nickname))
            ?? users.FirstOrDefault(u => Eq($"{u.FirstName} {u.LastName}"))
            ?? users.FirstOrDefault(u => Eq(u.FirstName));
        return match?.Id;
    }

    private byte[]? ReadPhoto(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)) return null;
        try
        {
            var path = _photoStorage.GetPath(fileName);
            return File.Exists(path) ? File.ReadAllBytes(path) : null;
        }
        catch { return null; }
    }

    // ---- mapping (mirrors the native MovieBak + the CSV transfer rules) ----

    private static MovieBak ToBak(Movie m)
    {
        var (seriesName, tags) = SplitTags(m.Tags);
        return new MovieBak
        {
            Id = m.Id,
            Title = m.Titel ?? string.Empty,
            Year = int.TryParse(m.Jahr, out var y) ? y : null,
            AgeRating = m.Fsk,
            Category = NormalizeCategory(m.Kategorie),
            Providers = SplitList(m.Anbieter),
            Tags = tags,
            Plot = null,                 // not stored on the web Movie entity
            RuntimeMinutes = m.Runtime,
            SeriesName = seriesName,
            Rating = null,               // no rating column on the web Movie entity
            Watched = false,
            PosterPath = m.PosterPath,
        };
    }

    private static Movie FromBak(MovieBak b, int householdId)
    {
        // Reconstruct the web Tags string: the series goes back to a leading '#'-tag.
        var tagParts = new List<string>();
        if (!string.IsNullOrWhiteSpace(b.SeriesName)) tagParts.Add("#" + b.SeriesName);
        tagParts.AddRange(b.Tags);

        return new Movie
        {
            Id = b.Id,
            HouseholdId = householdId,
            Titel = b.Title,
            Jahr = b.Year?.ToString(),
            Fsk = b.AgeRating,
            Kategorie = b.Category,
            Anbieter = b.Providers.Count > 0 ? string.Join(", ", b.Providers) : null,
            Tags = tagParts.Count > 0 ? string.Join(", ", tagParts) : null,
            PosterPath = b.PosterPath,
            Runtime = b.RuntimeMinutes,
        };
    }

    private static string NormalizeCategory(string? kategorie)
    {
        var k = (kategorie ?? string.Empty).ToLowerInvariant();
        if (k.Contains("tv")) return "tv";
        if (k.Contains("serie")) return "series";
        return "movie";
    }

    private static List<string> SplitList(string? commaJoined)
        => (commaJoined ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

    /// <summary>Splits the comma-joined Tags into the first '#'-series tag (without '#')
    /// and the remaining plain tags.</summary>
    private static (string? series, List<string> tags) SplitTags(string? commaJoined)
    {
        string? series = null;
        var tags = new List<string>();
        foreach (var raw in SplitList(commaJoined))
        {
            if (raw.StartsWith('#'))
            {
                series ??= raw.TrimStart('#');
            }
            else
            {
                tags.Add(raw);
            }
        }
        return (series, tags);
    }
}
